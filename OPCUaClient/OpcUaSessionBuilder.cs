using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace OPCUaClient
{
    /// <summary>
    /// A builder for creating OPC UA sessions.
    /// </summary>
    public sealed class OpcUaSessionBuilder
    {
        const string _applicationName = "LaMeR_Swarm_Worker";
        const string _subjectName = "CN=UA LaMeR_Swar_Worker, O=OPC Foundation, C=US, S=Arizona";
        const string _applicationUri = "urn:localhost:github.com:wolfi_by";
        const string _productUri = "Uri:github.com:wolfi_by";

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
            Identity = new UserIdentity(CertificateFactory
                .CreateCertificateWithPEMPrivateKey(
                new X509Certificate2(cert, key, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable), File.ReadAllBytes(key)));
            return this;
        }
        internal ISession Build()
        {
            return BuildAsync(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult();
        }

        internal async Task<ISessionChannel> BuildAsync(CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(_sessionName, nameof(_sessionName));
            ArgumentNullException.ThrowIfNull(Endpoint, nameof(Endpoint));

            var applicationInstance = new ApplicationInstance
            {
                ApplicationName = _applicationName,
                ApplicationType = ApplicationType.Client,
            };
            ApplicationConfiguration applicationConfiguration = await applicationInstance
                            .Build(_applicationUri, _productUri)
                            .AsClient()
                            .AddSecurityConfiguration(_subjectName)
                            .Create();

            var certOk = await applicationInstance.CheckApplicationInstanceCertificate(true, 0);

            Debug.Assert(certOk, "Certificate is not valid");

            try
            {
                applicationConfiguration.CertificateValidator.CertificateValidation += OnCertificateValidation;
                await applicationInstance.DeleteApplicationInstanceCertificate(cancellationToken);
                var endpoiintConfiguration = EndpointConfiguration.Create(applicationInstance);
                using var discoveryClient = DiscoveryClient.Create(new Uri(Endpoint));
                ConfiguredEndpoint configuredEndpoint = new(null, discoveryClient.Endpoint, discoveryClient.EndpointConfiguration);

                ISession session = await DefaultSessionFactory.Instance.CreateAsync(
                    applicationInstance,
                    configuredEndpoint,
                    UpdateBeforeConnect,
                    _sessionName,
                    SessionTimeout,
                    new UserIdentity(new AnonymousIdentityToken()),
                    preferredLocales: null,
                    cancellationToken);

                session.DeleteSubscriptionsOnClose = true;
                session.TransferSubscriptionsOnReconnect = true;

                return session;
            }
            catch (Exception)
            {
                applicationConfiguration.CertificateValidator.CertificateValidation -= OnCertificateValidation;
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
