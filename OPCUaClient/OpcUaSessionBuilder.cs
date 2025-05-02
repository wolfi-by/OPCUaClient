using Microsoft.AspNetCore.Server.Kestrel.Https;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using Org.BouncyCastle.Tls;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace OPCUaClient
{
    /// <summary>
    /// A builder for creating OPC UA sessions.
    /// </summary>
    public sealed class OpcUaSessionBuilder
    {
        const string _applicationName = "LaMeR_Swarm_Worker";
        const string _subjectName = "CN=UA LaMeR_Swarm_Worker, O=OPC Foundation, C=US, S=Arizona";
        const string _applicationUri = "urn:localhost:github.com:wolfi-by";
        const string _productUri = "Uri:github.com:wolfi-by";

        const string _baseSessionName = "LaMeR.Swarm.Worker";
        private string _sessionName = "LaMeR.Swarm.Worker";

        internal string? Endpoint;

        internal IUserIdentity Identity = new UserIdentity(new AnonymousIdentityToken());

        internal bool UseSecurity = true;
        internal bool DisableDomainCheck = true;
        internal bool UpdateBeforeConnect = true;

        internal int OperationTimeout = 60_000;
        internal uint SessionTimeout = 30_000;
        internal int KeepAliveInterval = 5_000;
        internal int ReconnectPeriod = 1_000;

        internal OpcUaSessionBuilder WithSessionName(string name)
        {
            _sessionName = $"{_baseSessionName}.{name}";
            return this;
        }

        internal OpcUaSessionBuilder WithEndpoint(string endpoint)
        {
            Endpoint = endpoint;
            return this;
        }

        internal OpcUaSessionBuilder WithTimeout(uint timeout)
        {
            SessionTimeout = timeout;
            return this;
        }

        internal OpcUaSessionBuilder WithKeepAliveInterval(int interval)
        {
            KeepAliveInterval = interval;
            return this;
        }

        internal OpcUaSessionBuilder WithAnonymousUserIdentity()
        {
            Identity = new UserIdentity(new AnonymousIdentityToken());
            return this;
        }

        internal OpcUaSessionBuilder WithUserIdentity(string userName, string password)
        {
            Identity = new UserIdentity(userName, password);
            return this;
        }
        internal OpcUaSessionBuilder WithCertificate(string cert, string key)
        {
            //Recheck how to use
            X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(File.ReadAllBytes(cert));

            Identity = new UserIdentity(CertificateFactory
                .CreateCertificateWithPEMPrivateKey(certificate, Encoding.UTF8.GetBytes(key), null));

            // new X509Certificate2(cert, key, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable), File.ReadAllBytes(key)));
            return this;
        }
        internal ISession Build()
        {
            return BuildAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult();
        }



        internal async Task<ISession> BuildAsync(CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(_sessionName, nameof(_sessionName));
            ArgumentNullException.ThrowIfNull(Endpoint, nameof(Endpoint));





            //var config = new ApplicationConfiguration()
            //{
            //    ApplicationName = _applicationName,
            //    ApplicationUri = _applicationUri, // Utils.Format(@"urn:{0}:" + _sessionName + "", Endpoint),
            //    ApplicationType = ApplicationType.Client,
            //    SecurityConfiguration = new SecurityConfiguration
            //    {
            //        ApplicationCertificate = new CertificateIdentifier { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault", SubjectName = Utils.Format(@"CN={0}, DC={1}", _applicationName, Endpoint) },
            //        TrustedIssuerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities" },
            //        TrustedPeerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications" },
            //        RejectedCertificateStore = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates" },
            //        AutoAcceptUntrustedCertificates = true,
            //        AddAppCertToTrustedStore = true
            //    },
            //    TransportConfigurations = new TransportConfigurationCollection(),
            //    TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
            //    ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
            //    TraceConfiguration = new TraceConfiguration(),
            //                };
            //config.Validate(ApplicationType.Client).GetAwaiter().GetResult();
            //if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            //{
            //    config.CertificateValidator.CertificateValidation += OnCertificateValidation;
            //}

            //var applicationInstance = new ApplicationInstance
            //{
            //    ApplicationName = _applicationName,
            //    ApplicationType = ApplicationType.Client,
            //    ApplicationConfiguration = config
            //};
            ////ApplicationConfiguration applicationConfiguration = await applicationInstance
            ////    .Build(_applicationUri, _productUri)
            ////    .AsClient().A(applicationConfiguration)
            ////    .Create();


            //var certOk = await applicationInstance.CheckApplicationInstanceCertificates(false, 2048);


            //// 
            //try
            //{
            //    //applicationConfiguration.CertificateValidator.CertificateValidation += OnCertificateValidation;
            //    await applicationInstance.DeleteApplicationInstanceCertificate(null, cancellationToken);
            //    using var discoveryClient = DiscoveryClient.Create(new Uri(Endpoint));
            //    ConfiguredEndpoint configuredEndpoint = new(null, discoveryClient.Endpoint, discoveryClient.EndpointConfiguration);
            //    var epDescription = configuredEndpoint.Description;
            //    ISession session = await Session.Create(config, new ConfiguredEndpoint(null, epDescription, EndpointConfiguration.Create(config)), UpdateBeforeConnect, "", SessionTimeout, null, null);

            //    //ISession session = await DefaultSessionFactory.Instance.CreateAsync(
            //    //    config,
            //    //    configuredEndpoint,
            //    //    UpdateBeforeConnect,
            //    //    _sessionName,
            //    //    SessionTimeout,
            //    //    new UserIdentity(new AnonymousIdentityToken()),
            //    //    preferredLocales: null,
            //    //    cancellationToken);

            //    session.DeleteSubscriptionsOnClose = true;
            //    session.TransferSubscriptionsOnReconnect = true;

            //    return session;
            //}
            //catch (Exception)
            //{
            //    config.CertificateValidator.CertificateValidation -= OnCertificateValidation;
            //    throw;
            //}

            Uri serverUri = new Uri(Endpoint);
            string serverAddress = serverUri.Host;//Dns.GetHostAddresses(serverUri.Host)[0].ToString();
            var config = new ApplicationConfiguration()
            {
                ApplicationName = "MyClient",
                ApplicationUri = Utils.Format(@"urn:{0}:MyClient", System.Net.Dns.GetHostName()),
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault", SubjectName = "MyClientSubjectName" },
                    TrustedIssuerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities" },
                    TrustedPeerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications" },
                    RejectedCertificateStore = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates" },
                    AutoAcceptUntrustedCertificates = true,
                    //AddAppCertToTrustedStore = true
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
                TraceConfiguration = new TraceConfiguration()
            };
            config.Validate(ApplicationType.Client).GetAwaiter().GetResult();
            if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            {
                config.CertificateValidator.CertificateValidation += (s, e) => { e.Accept = (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted); };
            }

            var application = new ApplicationInstance
            {
                ApplicationName = _applicationName,
                ApplicationType = ApplicationType.Client,
                ApplicationConfiguration = config
            };
            await application.CheckApplicationInstanceCertificates(false, 2048);

            EndpointDescription endpointDescription = CoreClientUtils.SelectEndpoint(Endpoint, false);
            EndpointConfiguration endpointConfiguration = EndpointConfiguration.Create(config);
            ConfiguredEndpoint endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);

            //using var discoveryClient = DiscoveryClient.Create(new Uri(Endpoint));
            //ConfiguredEndpoint configuredEndpoint = new(null, discoveryClient.Endpoint, discoveryClient.EndpointConfiguration);
            //var selectedEndpoint = configuredEndpoint.Description;
            try
            {
                ISession _session = Session.Create(config, endpoint, false, false, config.ApplicationName, 60000, new UserIdentity(), null).Result;
                return _session;
                ISession session = await DefaultSessionFactory.Instance.CreateAsync(
                    config,
                    endpoint,
                    UpdateBeforeConnect,
                    config.ApplicationName,
                    SessionTimeout,
                    new UserIdentity(new AnonymousIdentityToken()),
                    preferredLocales: null,
                    cancellationToken);

                return session;
            }
            catch (Exception ex)
            {

                config.CertificateValidator.CertificateValidation -= OnCertificateValidation;
                throw;
            }
        }



        private void OnCertificateValidation(CertificateValidator sender, CertificateValidationEventArgs e)
        {
            e.Accept = true;
            e.AcceptAll = true;
            sender.CertificateValidation -= OnCertificateValidation;
        }
    }
}
