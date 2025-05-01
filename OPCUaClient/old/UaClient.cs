using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using Opc.Ua.Gds;
using System.Threading.Tasks;
using Opc.Ua.Client.ComplexTypes;
using static Opc.Ua.RelativePathFormatter;
using System.Reflection;
using System.Diagnostics.Metrics;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.X509.Qualified;
using System.Collections;
using System;
using System.ComponentModel.DataAnnotations;
using static System.Collections.Specialized.BitVector32;
using System.Drawing;
using System.Dynamic;
using Microsoft.AspNetCore.DataProtection.Internal;
using OPCUaClient.old.Grok;
using OPCUaClient.old.Exceptions;
using OPCUaClient.old.Objects;



namespace OPCUaClient.old
{
    /// <summary>
    /// Client for OPCUA Server
    /// </summary>
    public class UaClient
    {
        #region Private Fields

        private readonly ConfiguredEndpoint _endpoint;
        private Session? _session = null;
        public ComplexTypeSystem? _complexTypeSystem = null;
        private readonly UserIdentity _userIdentity;
        private readonly ApplicationConfiguration _appConfig;
        private const int ReconnectPeriod = 10000;
        private readonly object _lock = new object();
        private SessionReconnectHandler? _reconnectHandler;

        #endregion

        #region Private methods

        private void Reconnect(object sender, EventArgs e)
        {
            if (!ReferenceEquals(sender, _reconnectHandler))
            {
                return;
            }

            lock (_lock)
            {
                if (_reconnectHandler.Session != null)
                {
                    _session = (Session)_reconnectHandler.Session;
                }

                _reconnectHandler.Dispose();
                _reconnectHandler = null;
            }
        }

        private Subscription Subscription(int miliseconds)
        {
            var subscription = new Subscription()
            {
                PublishingEnabled = true,
                PublishingInterval = miliseconds,
                Priority = 1,
                KeepAliveCount = 10,
                LifetimeCount = 20,
                MaxNotificationsPerPublish = 1000
            };

            return subscription;
        }

        #endregion

        #region Public fields

        /// <summary>
        /// Indicates if the instance is connected to the server.
        /// </summary>
        public bool IsConnected => _session is { Connected: true };

        #endregion

        #region Public methods

        /// <summary>
        /// Create a new instance
        /// </summary>
        /// <param name="appName">
        /// Name of the application
        /// </param>
        /// <param name="serverUrl">
        /// Url of server
        /// </param>
        /// <param name="security">
        /// Enable or disable the security
        /// </param>
        /// <param name="untrusted">
        /// Accept untrusted certificates
        /// </param>
        /// <param name="user">
        /// User of the OPC UA Server
        /// </param>
        /// <param name="password">
        /// Password of the user
        /// </param>
        public UaClient(string appName, string serverUrl, bool security, bool untrusted, string user = "",
            string password = "")
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Certificates");
            Directory.CreateDirectory(path);
            Directory.CreateDirectory(Path.Combine(path, "Application"));
            Directory.CreateDirectory(Path.Combine(path, "Trusted"));
            Directory.CreateDirectory(Path.Combine(path, "TrustedPeer"));
            Directory.CreateDirectory(Path.Combine(path, "Rejected"));
            string hostName = System.Net.Dns.GetHostName();

            _userIdentity = user.Length > 0 ? new UserIdentity(user, password) : new UserIdentity();
            _appConfig = new ApplicationConfiguration
            {
                ApplicationName = appName,
                ApplicationUri = Utils.Format(@"urn:{0}" + appName, hostName),
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StorePath = Path.Combine(path, "Application"),
                        SubjectName = $"CN={appName}, DC={hostName}"
                    },
                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = @"Directory",
                        StorePath = Path.Combine(path, "Trusted")
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = @"Directory",
                        StorePath = Path.Combine(path, "TrustedPeer")
                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = @"Directory",
                        StorePath = Path.Combine(path, "Rejected")
                    },
                    AutoAcceptUntrustedCertificates = true,
                    AddAppCertToTrustedStore = true,
                    RejectSHA1SignedCertificates = false
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 20000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 5000 },
                TraceConfiguration = new TraceConfiguration
                {
                    DeleteOnLoad = true,
                },
                DisableHiResClock = false
            };
            _appConfig.Validate(ApplicationType.Client).GetAwaiter().GetResult();

            if (_appConfig.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            {
                _appConfig.CertificateValidator.CertificateValidation += (s, ee) => { ee.Accept = ee.Error.StatusCode == StatusCodes.BadCertificateUntrusted && untrusted; };
            }

            var application = new ApplicationInstance
            {
                ApplicationName = appName,
                ApplicationType = ApplicationType.Client,
                ApplicationConfiguration = _appConfig
            };
            Utils.SetTraceMask(0);
            application.CheckApplicationInstanceCertificate(true, 2048).GetAwaiter().GetResult();

            var endpointDescription = CoreClientUtils.SelectEndpoint(_appConfig, serverUrl, security);
            var endpointConfig = EndpointConfiguration.Create(_appConfig);
            _endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfig);
        }

        /// <summary>
        /// Open the connection with the OPC UA Server
        /// </summary>
        /// <param name="timeOut">
        /// Timeout to try to connect with the server in seconds.
        /// </param>
        /// <param name="keepAlive">
        /// Sets whether to try to connect to the server in case the connection is lost.
        /// </param>
        /// <exception cref="ServerException"></exception>
        public void Connect(uint timeOut = 5, bool keepAlive = false)
        {
            Disconnect();

            _session =
                Task.Run(
                    async () => await Session.Create(_appConfig, _endpoint, false, false, _appConfig.ApplicationName,
                        timeOut * 1000, _userIdentity, null)).GetAwaiter().GetResult();

            if (keepAlive)
            {
                _session.KeepAlive += KeepAlive;
            }

            if (_session == null || !_session.Connected)
            {
                throw new ServerException("Error creating a session on the server");
            }
        }

        /// <summary>
        /// Open the connection with the OPC UA Server
        /// </summary>
        /// <param name="timeOut">
        /// Timeout to try to connect with the server in seconds.
        /// </param>
        /// <param name="keepAlive">
        /// Sets whether to try to connect to the server in case the connection is lost.
        /// </param>
        /// <param name="ct">
        /// CancellationToken to cancel the operation.
        /// </param>
        /// <exception cref="ServerException"></exception>
        public async Task ConnectAsync(uint timeOut = 5, bool keepAlive = false, CancellationToken ct = default)
        {
            await DisconnectAsync(ct);

            _session = await Session.Create(_appConfig, _endpoint, false, false, _appConfig.ApplicationName,
                timeOut * 1000, _userIdentity, null, ct);

            if (keepAlive)
            {
                _session.KeepAlive += KeepAlive;
            }

            if (_session == null || !_session.Connected)
            {
                throw new ServerException("Error creating a session on the server");
            }



            _complexTypeSystem = new ComplexTypeSystem(_session);

            await _complexTypeSystem.Load().ConfigureAwait(false);
            var types = _complexTypeSystem.GetDefinedTypes();
        }

        private void KeepAlive(ISession session, KeepAliveEventArgs e)
        {
            try
            {
                if (!ServiceResult.IsBad(e.Status)) return;
                lock (_lock)
                {
                    if (_reconnectHandler != null) return;
                    _reconnectHandler = new SessionReconnectHandler(true);
                    _reconnectHandler.BeginReconnect(_session, ReconnectPeriod, Reconnect);
                }
            }
            catch (Exception ex)
            {
                // ignored
            }
        }

        /// <summary>
        /// Close the connection with the OPC UA Server
        /// </summary>
        public void Disconnect()
        {
            if (_session is { Connected: true })
            {
                if (_session.Subscriptions != null && _session.Subscriptions.Any())
                {
                    foreach (var subscription in _session.Subscriptions)
                    {
                        subscription.Delete(true);
                    }
                }

                _session.Close();
                _session.Dispose();
                _session = null;
            }
        }

        /// <summary>
        /// Close the connection with the OPC UA Server
        /// </summary>
        public async Task DisconnectAsync(CancellationToken ct = default)
        {
            if (_session is { Connected: true })
            {
                if (_session.Subscriptions != null && _session.Subscriptions.Any())
                {
                    foreach (var subscription in _session.Subscriptions)
                    {
                        await subscription.DeleteAsync(true, ct);
                    }
                }

                await _session.CloseAsync(ct);
                _session.Dispose();
                _session = null;
            }
        }


        /// <summary>
        /// Write a value on a tag
        /// </summary>
        /// <param name="address">
        /// Address of the tag
        /// </param>
        /// <param name="value">
        /// Value to write
        /// </param>
        /// <exception cref="WriteException"></exception>
        public void Write(string address, object value)
        {
            WriteValueCollection writeValues = new WriteValueCollection();
            var writeValue = new WriteValue
            {
                NodeId = new NodeId(address),
                AttributeId = Attributes.Value,
                Value = new DataValue
                {
                    Value = value
                }
            };
            writeValues.Add(writeValue);
            _session.Write(null, writeValues, out StatusCodeCollection statusCodes,
                out DiagnosticInfoCollection diagnosticInfo);
            if (!StatusCode.IsGood(statusCodes[0]))
            {
                throw new WriteException("Error writing value. Code: " + statusCodes[0].Code.ToString());
            }
        }


        /// <summary>
        /// Write a value on a tag
        /// </summary>
        /// <param name="tag"> <see cref="Tag"/></param>
        /// <exception cref="WriteException"></exception>
        public void Write(Tag tag)
        {
            Write(tag.Address, tag.Value);
        }


        /// <summary>
        /// Read a tag of the sepecific address
        /// </summary>
        /// <param name="address">
        /// Address of the tag
        /// </param>
        /// <returns>
        /// <see cref="Tag"/>
        /// </returns>
        public Tag Read(string address)
        {
            var tag = new Tag
            {
                Address = address,
                Value = null,
            };
            ReadValueIdCollection readValues = new ReadValueIdCollection()
            {
                new ReadValueId
                {
                    NodeId = new NodeId(address),
                    AttributeId = Attributes.Value
                }
            };
            _session.Read(null, 0, TimestampsToReturn.Both, readValues, out DataValueCollection dataValues,
                out DiagnosticInfoCollection diagnosticInfo);


            tag.Value = dataValues[0].Value;
            tag.Code = dataValues[0].StatusCode;
            return tag;
        }




        /// <summary>
        /// Read an address
        /// </summary>
        /// <param name="address">
        /// Address to read.
        /// </param>
        /// <typeparam name="TValue">
        /// Type of value to read.
        /// </typeparam>
        /// <returns></returns>
        /// <exception cref="ReadException">
        /// If the status of read action is not good <see cref="StatusCodes"/>
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// If the type is not supported.
        /// </exception>
        public async Task<TValue> Read<TValue>(string address) where TValue : class
        {
            if (_session is null) return default!;
            var tag = new Tag
            {
                Address = address,
                Value = null,
            };
            {
                ReadValueIdCollection readValues = new ReadValueIdCollection()
            {
                new ReadValueId
                {
                    NodeId = new NodeId(address),
                    AttributeId = Attributes.Value
                }
            };


                _session.Read(null, 0, TimestampsToReturn.Both, readValues, out DataValueCollection dataValues,
                    out DiagnosticInfoCollection diagnosticInfo);

                if (dataValues[0].StatusCode != StatusCodes.Good)
                {
                    throw new ReadException(dataValues[0].StatusCode.Code.ToString());
                }

                var ct = CancellationToken.None;

                var response = await _session.ReadAsync(null, 0, TimestampsToReturn.Both, readValues, ct);

                var references = _session.FetchReferences(address);
                if (references != null)
                {

                    Type type = typeof(TValue);
                    object instance = (TValue)Activator.CreateInstance(typeof(TValue))!;

                    foreach (var reference in references.Where(r => r.NodeClass.ToString() == "Variable"))
                    {

                        NodeId nodeId = new NodeId(reference.NodeId.ToString());

                        PropertyInfo? prop = type.GetProperty(reference.DisplayName!.ToString());
                        if (!(prop is null))
                        {
                            var property = instance.GetType().GetProperty(reference.DisplayName!.ToString());
                            var isValue = property!.PropertyType.IsValueType || prop.PropertyType == typeof(string) || prop.PropertyType == typeof(DateTime);
                            var isEnumerable = property.PropertyType.BaseType == typeof(Array);
                            var isClass = property.PropertyType.IsClass && prop.PropertyType != typeof(string) && prop.PropertyType != typeof(DateTime) && property.PropertyType.BaseType != typeof(Array);

                            if (isClass)
                            {
                                Type type2 = prop.PropertyType.GetType();
                                object instance2 = Activator.CreateInstance(type2)!;
                                var classresult = typeof(UaClient).GetMethod("Read")?.MakeGenericMethod(prop.PropertyType).Invoke(instance2, new object[] { nodeId });
                                prop.SetValue(instance2, classresult, null);
                            }
                            else if (isValue)
                            {
                                var value = GetValueByType(prop.PropertyType, _session.ReadValue(nodeId));
                                //var value=Convert.ChangeType(_session.ReadValue(nodeId), prop.PropertyType);
                                prop.SetValue(instance, value, null);
                                Console.WriteLine(reference.BrowseName + " NodeId: " + reference.NodeId.ToString() + "value: " + value);
                            }
                            else if (isEnumerable)
                            {
                                Type type2 = property.ReflectedType!;
                                object instance2 = Activator.CreateInstance(type2)!;

                                // var classresult = await Read<type2>(nodeId.ToString());

                                //  var classresult = typeof(UaClient).GetMethod("Read")?.MakeGenericMethod(prop.PropertyType).Invoke(instance2, new object[] { nodeId });
                                // prop.SetValue(instance2, classresult, null);
                            }
                        }
                    }

                    return (TValue)instance;
                }
                return default!;


            }
        }


        private object GetValueByType(Type T, DataValue value)
        {
            if (T == typeof(bool))
            {
                return Convert.ToBoolean(value.Value);
            }
            else if (T == typeof(byte))
            {
                return Convert.ToByte(value.Value);
            }
            else if (T == typeof(ushort))
            {
                return Convert.ToUInt16(value.Value);
            }
            else if (T == typeof(uint))
            {
                return Convert.ToUInt32(value.Value);
            }
            else if (T == typeof(ulong))
            {
                return Convert.ToUInt64(value.Value);
            }
            else if (T == typeof(short))
            {
                return Convert.ToInt16(value.Value);
            }
            else if (T == typeof(int))
            {
                return Convert.ToInt32(value.Value);
            }
            else if (T == typeof(long))
            {
                return Convert.ToInt64(value.Value);
            }
            else if (T == typeof(float))
            {
                return Convert.ToSingle(value.Value);
            }
            else if (T == typeof(double))
            {
                return Convert.ToDouble(value.Value);
            }
            else if (T == typeof(decimal))
            {
                return Convert.ToDecimal(value.Value);
            }
            else if (T == typeof(string))
            {
                return Convert.ToString(value.Value) ?? string.Empty;
            }
            else if (T == typeof(DateTime))
            {
                return DateTime.Parse(value.Value.ToString() ?? string.Empty);
            }
            else if (T == typeof(Guid))
            {
                return Guid.Parse(value.Value.ToString() ?? string.Empty);
            }

            else
            {
                return default!;
            }
        }


        /// <summary>
        /// Write a lis of values
        /// </summary>
        /// <param name="tags"> <see cref="Tag"/></param>
        /// <exception cref="WriteException"></exception>
        public void Write(List<Tag> tags)
        {
            WriteValueCollection writeValues = new WriteValueCollection();


            writeValues.AddRange(tags.Select(tag => new WriteValue
            {
                NodeId = new NodeId(tag.Address, 2),
                AttributeId = Attributes.Value,
                Value = new DataValue()
                {
                    Value = tag.Value
                }
            }));
            _session.Write(null, writeValues, out StatusCodeCollection statusCodes,
                out DiagnosticInfoCollection diagnosticInfo);

            if (statusCodes.All(StatusCode.IsGood)) return;
            {
                var status = statusCodes.First(sc => !StatusCode.IsGood(sc));
                throw new WriteException("Error writing value. Code: " + status.Code.ToString());
            }
        }


        /// <summary>
        /// Read a list of tags on the OPCUA Server
        /// </summary>
        /// <param name="address">
        /// List of address to read.
        /// </param>
        /// <returns>
        /// A list of tags <see cref="Tag"/>
        /// </returns>
        public List<Tag> Read(List<string> address)
        {
            var tags = new List<Tag>();

            ReadValueIdCollection readValues = new ReadValueIdCollection();
            readValues.AddRange(address.Select(a => new ReadValueId
            {
                NodeId = new NodeId(a, 2),
                AttributeId = Attributes.Value
            }));

            _session.Read(null, 0, TimestampsToReturn.Both, readValues, out DataValueCollection dataValues,
                out DiagnosticInfoCollection diagnosticInfo);

            for (int i = 0; i < address.Count; i++)
            {
                tags.Add(new Tag
                {
                    Address = address[i],
                    Value = dataValues[i].Value,
                    Code = dataValues[i].StatusCode
                });
            }

            return tags;
        }


        /// <summary>
        /// Monitoring a tag and execute a function when the value change
        /// </summary>
        /// <param name="address">
        /// Address of the tag
        /// </param>
        /// <param name="miliseconds">
        /// Sets the time to check changes in the tag
        /// </param>
        /// <param name="monitor">
        /// Function to execute when the value changes.
        /// </param>
        public void Monitoring(string address, int miliseconds, MonitoredItemNotificationEventHandler monitor)
        {
            if (_session is null) return;
            var subscription = Subscription(miliseconds);
            MonitoredItem monitored = new MonitoredItem();
            monitored.StartNodeId = new NodeId(address);
            monitored.AttributeId = Attributes.Value;
            monitored.Notification += monitor;
            subscription.AddItem(monitored);
            _session.AddSubscription(subscription);
            subscription.Create();
            subscription.ApplyChanges();
        }

        /// <summary>
        /// Remove monitoring of a tag
        /// </summary>
        /// <param name="address"></param>
        public void RemoveMonitoring(string address)
        {
            if (_session is null) return;
            MonitoredItem monitored = new MonitoredItem();
            monitored.StartNodeId = new NodeId(address);
            monitored.AttributeId = Attributes.Value;
            var subscriptions = _session.Subscriptions;
            var itemsToRemove = subscriptions.Where(x => x.MonitoredItems.Any(y => y.StartNodeId == monitored.StartNodeId)).ToArray();
            _session.RemoveSubscriptions(itemsToRemove);

            //monitored.Notification += monitor;
            //subscription.AddItem(monitored);
            //this._session.AddSubscription(subscription);
            //subscription.Create();
            //subscription.ApplyChanges();
        }

        /// <summary>
        /// Scan root folder of OPC UA server and get all devices
        /// </summary>
        /// <param name="recursive">
        /// Indicates whether to search within device groups
        /// </param>
        /// <returns>
        /// List of <see cref="Device"/>
        /// </returns>
        public List<Device> Devices(bool recursive = false)
        {
            Browser browser = new Browser(_session)
            {
                BrowseDirection = BrowseDirection.Forward,
                NodeClassMask = (int)NodeClass.Object | (int)NodeClass.Variable,
                ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences
            };

            ReferenceDescriptionCollection browseResults = browser.Browse(Opc.Ua.ObjectIds.ObjectsFolder);

            var devices = browseResults.Where(d => d.ToString() != "Server").Select(b => new Device
            {
                Address = b.ToString()
            }).ToList();

            devices.ForEach(d =>
            {
                d.Groups = Groups(d.Address, recursive);
                d.Tags = Tags(d.Address);
            });

            return devices;
        }


        /// <summary>
        /// Scan an address and retrieve the tags and groups
        /// </summary>
        /// <param name="address">
        /// Address to search
        /// </param>
        /// <param name="recursive">
        /// Indicates whether to search within group groups
        /// </param>
        /// <returns>
        /// List of <see cref="Group"/>
        /// </returns>
        public List<Group> Groups(string address, bool recursive = false)
        {
            var groups = new List<Group>();
            Browser browser = new Browser(_session)
            {
                BrowseDirection = BrowseDirection.Forward,
                NodeClassMask = (int)NodeClass.Object | (int)NodeClass.Variable,
                ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences
            };

            ReferenceDescriptionCollection browseResults = browser.Browse(new NodeId(address));

            foreach (var result in browseResults)
            {
                if (result.NodeClass != NodeClass.Object) continue;
                var group = new Group
                {
                    Address = address + "." + result.ToString()
                };
                group.Groups = Groups(group.Address, recursive);
                group.Tags = Tags(group.Address);
                groups.Add(group);
            }

            return groups;
        }


        /// <summary>
        /// Scan an address and retrieve the tags.
        /// </summary>
        /// <param name="address">
        /// Address to search
        /// </param>
        /// <returns>
        /// List of <see cref="Tag"/>
        /// </returns>
        public List<Tag> Tags(string address)
        {
            var tags = new List<Tag>();
            Browser browser = new Browser(_session)
            {
                BrowseDirection = BrowseDirection.Forward,
                NodeClassMask = (int)NodeClass.Object | (int)NodeClass.Variable,
                ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences
            };

            ReferenceDescriptionCollection browseResults = browser.Browse(new NodeId(address));
            foreach (var result in browseResults)
            {
                if (result.NodeClass == NodeClass.Variable)
                {
                    tags.Add(new Tag
                    {
                        Address = address + "." + result.ToString()
                    });
                }
            }

            return tags;
        }


        #region Async methods

        /// <summary>
        /// Scan root folder of OPC UA server and get all devices
        /// </summary>
        /// <param name="recursive">
        /// Indicates whether to search within device groups
        /// </param>
        /// <param name="ct">
        /// Cancellation token
        /// </param>
        /// <returns>
        /// List of <see cref="Device"/>
        /// </returns>
        public Task<List<Device>> DevicesAsync(bool recursive = false, CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                Browser browser = new Browser(_session)
                {
                    BrowseDirection = BrowseDirection.Forward,
                    NodeClassMask = (int)NodeClass.Object | (int)NodeClass.Variable,
                    ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences
                };

                ReferenceDescriptionCollection browseResults = browser.Browse(Opc.Ua.ObjectIds.ObjectsFolder);

                var devices = browseResults.Where(d => d.ToString() != "Server").Select(b => new Device
                {
                    Address = b.ToString()
                }).ToList();

                devices.ForEach(d =>
                {
                    d.Groups = Groups(d.Address, recursive);
                    d.Tags = Tags(d.Address);
                });
                return devices;
            }, ct);
        }


        /// <summary>
        /// Scan an address and retrieve the tags and groups
        /// </summary>
        /// <param name="address">
        /// Address to search
        /// </param>
        /// <param name="recursive">
        /// Indicates whether to search within group groups
        /// </param>
        /// <param name="ct">
        /// Cancellation token
        /// </param>
        /// <returns>
        /// List of <see cref="Group"/>
        /// </returns>
        public Task<List<Group>> GroupsAsync(string address, bool recursive = false, CancellationToken ct = default)
        {
            return Task.Run(() => Groups(address, recursive), ct);
        }


        /// <summary>
        /// Scan an address and retrieve the tags.
        /// </summary>
        /// <param name="address">
        /// Address to search
        /// </param>
        /// <param name="ct">
        ///  Cancellation token
        /// </param>
        /// <returns>
        /// List of <see cref="Tag"/>
        /// </returns>
        public Task<List<Tag>> TagsAsync(string address, CancellationToken ct = default)
        {
            return Task.Run(() => Tags(address), ct);
        }


        /// <summary>
        /// Write a value on a tag
        /// </summary>
        /// <param name="address">
        /// Address of the tag
        /// </param>
        /// <param name="value">
        /// Value to write
        /// </param>
        /// <param name="ct">
        /// Cancellation token
        /// </param>
        public async Task<Tag> WriteAsync(string address, object value, CancellationToken ct = default)
        {
            WriteValueCollection writeValues = new WriteValueCollection();
            var writeValue = new WriteValue
            {
                NodeId = new NodeId(address),
                AttributeId = Attributes.Value,
                Value = new DataValue
                {
                    Value = value
                }
            };
            writeValues.Add(writeValue);
            WriteResponse response = await _session.WriteAsync(null, writeValues, ct);

            var tag = new Tag()
            {
                Address = address,
                Value = value,
                Code = response.Results[0].Code
            };

            return tag;
        }


        /// <summary>
        /// Write a value on a tag
        /// </summary>
        /// <param name="tag"> <see cref="Tag"/></param>
        /// <param name="ct"> Cancellation token</param>
        public Task<Tag> WriteAsync(Tag tag, CancellationToken ct = default)
        {
            var task = WriteAsync(tag.Address, tag.Value, ct);

            return task;
        }

        /// <summary>
        /// Write a lis of values
        /// </summary>
        /// <param name="tags"><see cref="Tag"/></param>
        /// <param name="ct">
        /// Cancellation token
        /// </param>
        public async Task<IEnumerable<Tag>> WriteAsync(List<Tag> tags, CancellationToken ct = default)
        {
            WriteValueCollection writeValues = new WriteValueCollection();


            writeValues.AddRange(tags.Select(tag => new WriteValue
            {
                NodeId = new NodeId(tag.Address, 2),
                AttributeId = Attributes.Value,
                Value = new DataValue()
                {
                    Value = tag.Value
                }
            }));

            WriteResponse response = await _session.WriteAsync(null, writeValues, ct);

            for (int i = 0; i < response.Results.Count; i++)
            {
                tags[i].Code = response.Results[i].Code;
            }

            return tags;
        }


        /// <summary>
        /// Read a tag of the specific address
        /// </summary>
        /// <param name="address">
        /// Address of the tag
        /// </param>
        /// <param name="ct">
        /// Cancellation token
        /// </param>
        /// <returns>
        /// <see cref="Tag"/>
        /// </returns>
        public async Task<Tag> ReadAsync(string address, CancellationToken ct = default)
        {
            var tag = new Tag
            {
                Address = address,
                Value = null,
            };
            ReadValueIdCollection readValues = new ReadValueIdCollection()
            {
                new ReadValueId
                {
                    NodeId = new NodeId(address),
                    AttributeId = Attributes.Value
                }
            };

            var dataValues = await _session.ReadAsync(null, 0, TimestampsToReturn.Both, readValues, ct);

            tag.Value = dataValues.Results[0].Value;
            tag.Code = dataValues.Results[0].StatusCode;

            return tag;
        }

        /// <summary>
        /// Read an address
        /// </summary>
        /// <param name="address">
        /// Address to read.
        /// </param>
        /// <param name="ct">
        ///  Cancellation token
        /// </param>
        /// <typeparam name="TValue">
        /// Type of value to read.
        /// </typeparam>
        /// <returns></returns>
        /// <exception cref="ReadException">
        /// If the status of read action is not good <see cref="StatusCodes"/>
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// If the type is not supported.
        /// </exception>
        public Task<TValue> ReadAsync<TValue>(string address, CancellationToken ct = default) where TValue : class
        {
            return Task.Run(() => Read<TValue>(address), ct);
        }


        /// <summary>
        /// Read a list of tags on the OPCUA Server
        /// </summary>
        /// <param name="address">
        /// List of address to read.
        /// </param>
        /// <param name="ct">
        ///  Cancellation token
        /// </param>
        /// <returns>
        /// A list of tags <see cref="Tag"/>
        /// </returns>
        public async Task<IEnumerable<Tag>> ReadAsync(IEnumerable<string> address, CancellationToken ct = default)
        {
            var tags = new List<Tag>();

            ReadValueIdCollection readValues = new ReadValueIdCollection();
            readValues.AddRange(address.Select(a => new ReadValueId
            {
                NodeId = new NodeId(a, 2),
                AttributeId = Attributes.Value
            }));

            var dataValues =
                await _session.ReadAsync(null, 0, TimestampsToReturn.Both, readValues, ct);

            for (int i = 0; i < dataValues.Results.Count; i++)
            {
                tags.Add(new Tag
                {
                    Address = address.ToArray()[i],
                    Value = dataValues.Results[i].Value,
                    Code = dataValues.Results[i].StatusCode
                });
            }

            return tags;
        }

        #endregion

        #endregion

        #region Info from Grok
        //https://x.com/i/grok/share/tfUisFdmJWVXfX7XEyBgrhWOd
        /// <summary>
        /// 
        /// </summary>
        /// <param name="t"></param>
        /// <param name="bytes"></param>
        /// <param name="decoder"></param>
        /// <returns></returns>
        static object DeserializeTo(Type t, byte[] bytes, BinaryDecoder decoder)
        {
            var outputInstance = Activator.CreateInstance(t);
            var properties = t.GetTypeInfo().GetProperties();

            foreach (var p in properties)
            {
                object? value = p.PropertyType switch
                {
                    Type { Name: nameof(Int16) } => decoder.ReadInt16(p.Name),
                    Type { Name: "Int16[]" } => decoder.ReadInt16Array(p.Name),
                    /* etc. */
                    Type { IsClass: true } childType => DeserializeTo(childType, bytes, decoder),
                    _ => null
                };
                p.SetValue(outputInstance, value);
            }

            return outputInstance;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TOutput"></typeparam>
        /// <param name="bytes"></param>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public TOutput FromBytes<TOutput>(byte[] bytes, IServiceMessageContext ctx)
        {
            var decoder = new BinaryDecoder(bytes, ctx);
            var outputResult = DeserializeTo(typeof(TOutput), bytes, decoder);
            return (TOutput)outputResult;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="nodeId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<T> ReadNodeData<T>(NodeId nodeId) where T : class, new()
        {
            if (_session is null) return default!;
            if (_session.Connected == false) return default!;
            // Ermittle die DataTypeId der Node
            var node = ExpandedNodeId.Parse(nodeId.ToString(), _session.NamespaceUris);
            var result = _session.ReadNode(node);

            

            var browseDescription = new BrowseDescription
            {
                NodeId = nodeId,
                BrowseDirection = BrowseDirection.Forward,
                ReferenceTypeId = ReferenceTypeIds.HasTypeDefinition, // Alternativ: direkt nach DataType suchen
                IncludeSubtypes = true,
                NodeClassMask = (uint)NodeClass.DataType, // Nur VariableNodes
                ResultMask = (uint)BrowseResultMask.All
            };

            _session.Browse(null, null, 0, new BrowseDescriptionCollection { browseDescription }, out BrowseResultCollection browseResults, out DiagnosticInfoCollection diagnostics);

            if (browseResults == null || browseResults.Count == 0)
            {
                throw new Exception($"Konnte die Node {nodeId} nicht finden.");
            }

            var outputInstance = Activator.CreateInstance(typeof(T));
            if (outputInstance == null)
            {
                return default!;
            }
            var properties=outputInstance.GetType().GetProperties();








            //EncodeableFactory.GlobalFactory.AddEncodeableType(typeof(T));
            //DataValue dv = _session.Read(nodeId);
            //var rest = ExtensionObject.ToEncodeable((ExtensionObject)dv.Value) as T;
            //var obj = (ExtensionObject)dv.Value;
            //var value1 = (T)obj.Body;


            //   var conversion=FromBytes<T>(data.Value as byte[],_session.MessageContext);



            //var dataTypeId = await GetDataTypeId(_session, nodeId);


            return default!;

        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="nodeId"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task WriteNodeData<T>(NodeId nodeId, T value) where T : class
        {
            if (_session is null) return;
            if (_session.Connected == false) return;
            // Ermittle die DataTypeId der Node
            var dataTypeId = await GetDataTypeId(_session, nodeId);

            var nodesToWrite = new WriteValueCollection();

            if (value != null && value.GetType().IsClass && !(value is string))
            {
                // Konvertiere die C#-Klasse in ein ExtensionObject
                var extensionObject = OpcUaMapper.MapToOpcUa(value, dataTypeId);
                var writeValue = new WriteValue
                {
                    NodeId = nodeId,
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(extensionObject))
                };
                nodesToWrite.Add(writeValue);
            }
            else
            {
                // Elementarer Datentyp
                var writeValue = new WriteValue
                {
                    NodeId = nodeId,
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(value))
                };
                nodesToWrite.Add(writeValue);
            }

            // Schreibe die Daten
            _session.Write(null, nodesToWrite, out StatusCodeCollection statusCodes, out DiagnosticInfoCollection diagnostics);

            if (!StatusCode.IsGood(statusCodes[0]))
            {
                throw new Exception($"Fehler beim Schreiben der Node {nodeId}: {statusCodes[0]}");
            }
        }

        public async Task<NodeId> GetDataTypeId(Session session, NodeId nodeId)
        {
            var factory = new EncodeableFactory();
            var types = factory.EncodeableTypes;

            // Browse-Operation, um die DataType-Referenz der Node zu finden
            var browseDescription = new BrowseDescription
            {
                NodeId = nodeId,
                BrowseDirection = BrowseDirection.Forward,
                ReferenceTypeId = ReferenceTypeIds.HasTypeDefinition, // Alternativ: direkt nach DataType suchen
                IncludeSubtypes = true,
                NodeClassMask = (uint)NodeClass.DataType, // Nur VariableNodes
                ResultMask = (uint)BrowseResultMask.All
            };

            session.Browse(null, null, 0, new BrowseDescriptionCollection { browseDescription }, out BrowseResultCollection browseResults, out DiagnosticInfoCollection diagnostics);

            if (browseResults == null || browseResults.Count == 0)
            {
                throw new Exception($"Konnte die Node {nodeId} nicht finden.");
            }



            var expandedNode = ExpandedNodeId.ToNodeId(browseResults[0].TypeId, session.NamespaceUris);
            var myValue = types.TryGetValue(expandedNode, out var myType2);
            if (myType2 != null)
            {
                var sss = myType2.GetTypeInfo();
                var aaa = myType2.ReflectedType;

            }

            // Prüfe die Referenzen, um den Datentyp zu finden
            foreach (var reference in browseResults[0].References)
            {
                // Hole die NodeClass und weitere Details der referenzierten Node
                var targetNodeId = ExpandedNodeId.ToNodeId(reference.NodeId, session.NamespaceUris);
                var trygetvalue = types.TryGetValue(targetNodeId, out var myType);



                var detail = new BrowseDescription
                {
                    NodeId = targetNodeId,
                    BrowseDirection = BrowseDirection.Forward,
                    ReferenceTypeId = ReferenceTypeIds.HasTypeDefinition,
                    IncludeSubtypes = true,
                    NodeClassMask = (uint)NodeClass.ReferenceType,
                    ResultMask = (uint)BrowseResultMask.All
                };
                var detailResult = session.ReadNode(targetNodeId);

                var nodeDetails = new BrowseDescription
                {
                    NodeId = targetNodeId,
                    BrowseDirection = BrowseDirection.Forward,
                    ReferenceTypeId = ReferenceTypeIds.HasTypeDefinition,
                    IncludeSubtypes = true,
                    NodeClassMask = (uint)NodeClass.DataType,
                    ResultMask = (uint)BrowseResultMask.All
                };

                session.Browse(null, null, 0, new BrowseDescriptionCollection { nodeDetails }, out BrowseResultCollection typeResults, out DiagnosticInfoCollection typeDiagnostics);

                if (typeResults != null && typeResults.Count > 0)
                {
                    foreach (var typeRef in typeResults[0].References)
                    {
                        var dataTypeId = ExpandedNodeId.ToNodeId(typeRef.NodeId, session.NamespaceUris);
                        if (dataTypeId != null)
                        {
                            return dataTypeId; // Datentyp gefunden
                        }
                    }
                }

                // Alternativ: Direkt nach dem DataType-Attribut der Node browsen
                var dataTypeBrowse = new BrowseDescription
                {
                    NodeId = nodeId,
                    BrowseDirection = BrowseDirection.Forward,
                    ReferenceTypeId = ReferenceTypeIds.HasProperty, // DataType könnte als Property vorliegen
                    IncludeSubtypes = true,
                    NodeClassMask = (uint)NodeClass.Variable,
                    ResultMask = (uint)BrowseResultMask.All
                };

                session.Browse(null, null, 0, new BrowseDescriptionCollection { dataTypeBrowse }, out BrowseResultCollection dataTypeResults, out DiagnosticInfoCollection dataTypeDiagnostics);

                foreach (var refDescription in dataTypeResults[0].References)
                {
                    var refNodeId = ExpandedNodeId.ToNodeId(refDescription.NodeId, session.NamespaceUris);
                    if (refDescription.BrowseName.Name == "DataType")
                    {
                        return refNodeId; // Datentyp gefunden
                    }
                }
            }
            return default!;
            throw new Exception($"Konnte die DataTypeId der Node {nodeId} nicht ermitteln.");
        }
        #endregion

        #region Beispiel Grok

        //        // Beispiel: Daten lesen
        //        var nodeId = new NodeId("ns=2;s=MyVectorNode");
        //        var dataTypeId = new NodeId("ns=2;s=Vector");
        //        var vector = await ReadNodeData<Vector>(session, nodeId, dataTypeId);
        //        Console.WriteLine($"Vector: X={vector.X}, Y={vector.Y}, Z={vector.Z}");

        //// Beispiel: Daten schreiben
        //var newVector = new Vector { X = 1.0, Y = 2.0, Z = 3.0 };
        //        await WriteNodeData(session, nodeId, dataTypeId, newVector);
        #endregion
    }
}