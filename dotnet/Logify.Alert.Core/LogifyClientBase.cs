﻿using DevExpress.Logify.Core.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
//using System.Configuration;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
//using System.Security.Cryptography;
//using System.Text;

namespace DevExpress.Logify.Core {
    public abstract class LogifyClientBase {
        string serviceUrl = "https://logify.devexpress.com/api/report/";
        string apiKey;
        bool confirmSendReport;
        //string miniDumpServiceUrl;
        string offlineReportsDirectory = "offline_reports";
        int offlineReportsCount = 100;
        bool offlineReportsEnabled;
        ICredentials proxyCredentials;
#if NETSTANDARD
        IWebProxy proxy;
#endif

        public static LogifyClientBase Instance { get; protected set; }

        ILogifyClientConfiguration config;
        IDictionary<string, string> customData = new Dictionary<string, string>();
        IStackTraceHelper stackTraceHelper;
        BreadcrumbCollection breadcrumbs = new BreadcrumbCollection();
        AttachmentCollection attachments = new AttachmentCollection();

        protected LogifyClientBase() {
            Init(null);
        }
        protected LogifyClientBase(string apiKey) {
            Init(null);
            this.ApiKey = apiKey;
        }
        protected LogifyClientBase(Dictionary<string, string> config) {
            Init(config);
        }

        public string ServiceUrl {
            get { return serviceUrl; }
            set {
                serviceUrl = value;
                IExceptionReportSender sender = ExceptionLoggerFactory.Instance.PlatformReportSender;
                if (sender != null)
                    sender.ServiceUrl = value;
            }
        }
        public string ApiKey {
            get { return apiKey; }
            set {
                apiKey = value;
                IExceptionReportSender sender = ExceptionLoggerFactory.Instance.PlatformReportSender;
                if (sender != null)
                    sender.ApiKey = value;
            }
        }
        public bool ConfirmSendReport {
            get { return confirmSendReport; }
            set {
                confirmSendReport = value;
                IExceptionReportSender sender = ExceptionLoggerFactory.Instance.PlatformReportSender;
                if (sender != null)
                    sender.ConfirmSendReport = value;
            }
        }
#if NETSTANDARD
        /*public*/ IWebProxy Proxy {
            get { return proxy; }
            set {
                proxy = value;
                IExceptionReportSender sender = ExceptionLoggerFactory.Instance.PlatformReportSender;
                if (sender != null)
                    sender.Proxy = value;
            }
        }
#endif
        public ICredentials ProxyCredentials {
            get { return proxyCredentials; }
            set {
                proxyCredentials = value;
                IExceptionReportSender sender = ExceptionLoggerFactory.Instance.PlatformReportSender;
                if (sender != null)
                    sender.ProxyCredentials = value;
            }
        }
        /*
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string MiniDumpServiceUrl {
            get { return miniDumpServiceUrl; }
            set {
                miniDumpServiceUrl = value;
                IExceptionReportSender sender = ExceptionLoggerFactory.Instance.PlatformReportSender;
                if (sender != null)
                    sender.MiniDumpServiceUrl = value;
            }
        }
        */

        public string OfflineReportsDirectory {
            get { return offlineReportsDirectory; }
            set {
                offlineReportsDirectory = value;
                ApplyRecursively<IOfflineDirectoryExceptionReportSender>(ExceptionLoggerFactory.Instance.PlatformReportSender, (s) => { s.DirectoryName = value; });
            }
        }
        public int OfflineReportsCount {
            get { return offlineReportsCount; }
            set {
                offlineReportsCount = value;
                ApplyRecursively<IOfflineDirectoryExceptionReportSender>(ExceptionLoggerFactory.Instance.PlatformReportSender, (s) => { s.ReportCount = value; });
            }
        }
        public bool OfflineReportsEnabled {
            get { return offlineReportsEnabled; }
            set {
                offlineReportsEnabled = value;
                ApplyRecursively<IOfflineDirectoryExceptionReportSender>(ExceptionLoggerFactory.Instance.PlatformReportSender, (s) => { s.IsEnabled = value; });
            }
        }

        public string AppName { get; set; }
        public string AppVersion { get; set; }
        public string UserId { get; set; }
        public IDictionary<string, string> CustomData { get { return customData; } }
        public AttachmentCollection Attachments { get { return attachments; } }
        public BreadcrumbCollection Breadcrumbs { get { return breadcrumbs; } }
        protected internal bool CollectBreadcrumbsCore {
            get { return Config.CollectBreadcrumbs; }
            set {
                Config.CollectBreadcrumbs = value;
                EndCollectBreadcrumbs();
                BeginCollectBreadcrumbs();
            }
        }
        protected internal int BreadcrumbsMaxCountCore {
            get { return Config.BreadcrumbsMaxCount; }
            set {
                if (this.BreadcrumbsMaxCountCore == value)
                    return;

                if (BreadcrumbsMaxCountCore <= 1)
                    throw new ArgumentException();

                Config.BreadcrumbsMaxCount = value;
                ForceUpdateBreadcrumbsMaxCount();
            }
        }
        protected internal void ForceUpdateBreadcrumbsMaxCount() {
            this.breadcrumbs = BreadcrumbCollection.ChangeSize(this.Breadcrumbs, Config.BreadcrumbsMaxCount);
        }
        protected bool IsSecondaryInstance { get; set; }

        protected internal ILogifyClientConfiguration Config { get { return config; } }

        //internal NetworkCredential ProxyCredentials { get; set; }

        CanReportExceptionEventHandler onCanReportException;
        public event CanReportExceptionEventHandler CanReportException { add { onCanReportException += value; } remove { onCanReportException -= value; } }
        bool RaiseCanReportException(Exception ex) {
            if (onCanReportException != null) {
                CanReportExceptionEventArgs args = new CanReportExceptionEventArgs();
                args.Exception = ex;
                onCanReportException(this, args);
                return !args.Cancel;
            } else
                return true;
        }

        BeforeReportExceptionEventHandler onBeforeReportException;
        public event BeforeReportExceptionEventHandler BeforeReportException { add { onBeforeReportException += value; } remove { onBeforeReportException -= value; } }
        void RaiseBeforeReportException(Exception ex) {
            if (onBeforeReportException != null) {
                BeforeReportExceptionEventArgs args = new BeforeReportExceptionEventArgs();
                args.Exception = ex;
                onBeforeReportException(this, args);
            }
        }
        AfterReportExceptionEventHandler onAfterReportException;
        public event AfterReportExceptionEventHandler AfterReportException { add { onAfterReportException += value; } remove { onAfterReportException -= value; } }
        void RaiseAfterReportException(Exception ex) {
            if (onAfterReportException != null) {
                AfterReportExceptionEventArgs args = new AfterReportExceptionEventArgs();
                args.Exception = ex;
                onAfterReportException(this, args);
            }
        }

        void ApplyRecursively<TSender>(IExceptionReportSender sender, Action<TSender> action) where TSender : class {
            if (sender == null)
                return;
            TSender typedSender = sender as TSender;
            if (typedSender != null)
                action(typedSender);

            IExceptionReportSenderWrapper wrapper = sender as IExceptionReportSenderWrapper;
            if (wrapper != null)
                ApplyRecursively<TSender>(wrapper.InnerSender, action);

            CompositeExceptionReportSender composite = sender as CompositeExceptionReportSender;
            if (composite != null) {
                if (composite.Senders != null && composite.Senders.Count > 0) {
                    int count = composite.Senders.Count;
                    for (int i = 0; i < count; i++)
                        ApplyRecursively<TSender>(composite.Senders[i], action);
                }
            }
        }

        void Init(Dictionary<string, string> configDictionary) {
            this.IsSecondaryInstance = DetectIfSecondaryInstance();
            //this.innerConfig = CreateClientConfiguration(); // new DefaultClientConfiguration();
            this.stackTraceHelper = CreateStackTraceHelper();
            this.config = new DefaultClientConfiguration();
            this.ConfirmSendReport = false; // do not confirm by default

            IExceptionReportSender reportSender = CreateExceptionReportSender();
            Configure();
            InitAfterConfigure(reportSender);
        }
        protected internal void InitAfterConfigure() {
            InitAfterConfigure(CreateExceptionReportSender());
        }
        protected void InitAfterConfigure(IExceptionReportSender reportSender) {
            reportSender.ServiceUrl = this.ServiceUrl;
            reportSender.ApiKey = this.ApiKey;
            reportSender.ConfirmSendReport = this.ConfirmSendReport;
            reportSender.ProxyCredentials = this.ProxyCredentials;
#if NETSTANDARD
            reportSender.Proxy = this.Proxy;
#endif
            //reportSender.MiniDumpServiceUrl = this.MiniDumpServiceUrl;
            ApplyRecursively<IOfflineDirectoryExceptionReportSender>(reportSender, (s) => { s.IsEnabled = this.OfflineReportsEnabled; });
            ApplyRecursively<IOfflineDirectoryExceptionReportSender>(reportSender, (s) => { s.DirectoryName = this.OfflineReportsDirectory; });
            ApplyRecursively<IOfflineDirectoryExceptionReportSender>(reportSender, (s) => { s.ReportCount = this.OfflineReportsCount; });


            //TODO:
            //apply values to config

            ExceptionLoggerFactory.Instance.PlatformReportSender = CreateBackgroundExceptionReportSender(reportSender);
            ExceptionLoggerFactory.Instance.PlatformCollectorFactory = CreateCollectorFactory();
            ExceptionLoggerFactory.Instance.PlatformIgnoreDetection = CreateIgnoreDetection();
        }

        protected virtual bool DetectIfSecondaryInstance() {
            return false;
        }

        protected abstract IExceptionReportSender CreateExceptionReportSender();
        protected abstract IInfoCollectorFactory CreateCollectorFactory();
        protected abstract IExceptionIgnoreDetection CreateIgnoreDetection();
        protected abstract string GetAssemblyVersionString(Assembly asm);
        //protected abstract ILogifyClientConfiguration CreateClientConfiguration();
        protected abstract void Configure();
        protected abstract IInfoCollector CreateDefaultCollector(IDictionary<string, string> additionalCustomData, AttachmentCollection additionalAttachments);
        protected abstract BackgroundExceptionReportSender CreateBackgroundExceptionReportSender(IExceptionReportSender reportSender);
        protected abstract IExceptionReportSender CreateEmptyPlatformExceptionReportSender();
        protected abstract ISavedReportSender CreateSavedReportsSender();
        protected internal abstract ReportConfirmationModel CreateConfirmationModel(LogifyClientExceptionReport report, Func<LogifyClientExceptionReport, bool> sendAction);
        protected abstract IStackTraceHelper CreateStackTraceHelper();

        protected internal abstract bool RaiseConfirmationDialogShowing(ReportConfirmationModel model);
        protected void BeginCollectBreadcrumbs() {
            if (CollectBreadcrumbsCore)
                BeginCollectBreadcrumbsCore();
        }
        protected void EndCollectBreadcrumbs() {
            EndCollectBreadcrumbsCore();
        }

        protected virtual void BeginCollectBreadcrumbsCore() {
        }
        protected virtual void EndCollectBreadcrumbsCore() {
        }
        public abstract void Run();
        public abstract void Stop();

        public void SendOfflineReports() {
            try {
                if (!OfflineReportsEnabled)
                    return;

                IExceptionReportSender innerSender = CreateEmptyPlatformExceptionReportSender();
                if (innerSender == null)
                    return;

                innerSender.ConfirmSendReport = false;
                innerSender.ProxyCredentials = this.proxyCredentials;
#if NETSTANDARD
                innerSender.Proxy = this.Proxy;
#endif
                innerSender.ApiKey = this.ApiKey;
                innerSender.ServiceUrl = this.ServiceUrl;
                //innerSender.MiniDumpServiceUrl = this.MiniDumpServiceUrl;

                ISavedReportSender savedReportsSender = CreateSavedReportsSender();
                if (savedReportsSender == null)
                    return;

                savedReportsSender.Sender = innerSender;
                savedReportsSender.DirectoryName = this.OfflineReportsDirectory;
                savedReportsSender.TrySendOfflineReports();
            } catch {
            }
        }

        public void StartExceptionsHandling() {
            Run();
        }
        public void StopExceptionsHandling() {
            Stop();
        }

        //const string serviceInfo = "/RW+Wzq8wasJP6LuHZcAbT2ShAvheOdnptsr/RI8zeCCfF6a+zXeWOhG0STFbxoLDjpzWj49DMTp0KZXufp4gz45nsSUwhcnrJC280vWliI=";
        protected void ReportToDevExpressCore(string uniqueUserId, string lastExceptionReportFileName, Assembly asm, IDictionary<string, string> customData) {
            IExceptionReportSender sender = ExceptionLoggerFactory.Instance.PlatformReportSender;
            if (sender != null && sender.CanSendExceptionReport())
                return;

            IExceptionReportSender reportSender = CreateExceptionReportSender();

            CompositeExceptionReportSender compositeSender = reportSender as CompositeExceptionReportSender;
            if (compositeSender == null) {
                compositeSender = new CompositeExceptionReportSender();
                compositeSender.Senders.Add(reportSender);
            }

            /*if (!String.IsNullOrEmpty(lastExceptionReportFileName)) {
                FileExceptionReportSender fileSender = new FileExceptionReportSender();
                fileSender.FileName = lastExceptionReportFileName;
                compositeSender.Senders.Add(fileSender);
            }*/
            string[] info = GetServiceInfo(asm);
            if (info != null && info.Length == 2) {
                this.ServiceUrl = info[0]; // "http://logify.devexpress.com/api/report/";
                this.ApiKey = info[1]; // "12345678FEE1DEADBEEF4B1DBABEFACE";
                //if (this.ServiceUrl.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase)) {
                if (CultureInfo.InvariantCulture.CompareInfo.IsPrefix(this.ServiceUrl, "http://", CompareOptions.IgnoreCase)) {
                    this.ServiceUrl = "https://" + this.ServiceUrl.Substring("http://".Length);
                }
            }
            //this.MiniDumpServiceUrl = "http://logifydump.devexpress.com/";
            compositeSender.ServiceUrl = this.ServiceUrl;
            //compositeSender.ApiKey = "dx$" + logId;
            compositeSender.ApiKey = this.ApiKey;
            //compositeSender.MiniDumpServiceUrl = this.MiniDumpServiceUrl;
            this.AppName = "DevExpress Demo or Design Time";
            this.AppVersion = DetectDevExpressVersion(asm);
            this.UserId = uniqueUserId;
            this.ConfirmSendReport = false;
            this.CollectBreadcrumbsCore = false;
            if (customData != null)
                this.customData = customData;

            //TODO:
            Config.CollectMiniDump = true;

            //apply values to config

            ExceptionLoggerFactory.Instance.PlatformReportSender = CreateBackgroundExceptionReportSender(compositeSender);
        }

        string DetectDevExpressVersion(Assembly asm) {
            if (asm == null)
                return String.Empty;

            return GetAssemblyVersionString(asm);
            //return asm.GetName().Version.ToString();
        }
        string[] GetServiceInfo(Assembly asm) {
            if (asm == null)
                return new string[2];

            string name = asm.FullName;
            int index = name.LastIndexOf('=');
            if (index < 0)
                return new string[2];

#if DEBUG
            string password = "b88d1754d700e49a";
#else
            string password = name.Substring(index + 1);
#endif

            if (password == "b88d1754d700e49a")
                return new string[] { "https://logify.devexpress.com/api/report/", "12345678FEE1DEADBEEF4B1DBABEFACE" };

            return new string[2];
        }
        /*
        string[] GetServiceInfo(Assembly asm) {
            if (asm == null)
                return new string[2];
            byte[] data = Convert.FromBase64String(serviceInfo);
            MemoryStream stream = new MemoryStream(data);

            string name = asm.FullName;
            int index = name.LastIndexOf('=');
            if (index < 0)
                return new string[2];

#if DEBUG
            string password = "b88d1754d700e49a";
#else
            string password = name.Substring(index + 1);
#endif
            Aes crypt = Aes.Create();
            ICryptoTransform transform = crypt.CreateDecryptor(new PasswordDeriveBytes(Encoding.UTF8.GetBytes(password), null).GetBytes(16), new byte[16]);
            CryptoStream cryptStream = new CryptoStream(stream, transform, CryptoStreamMode.Read);
            BinaryReader reader = new BinaryReader(cryptStream);
            uint crc = reader.ReadUInt32();
            short length = reader.ReadInt16();
            byte[] bytes = reader.ReadBytes(length);

            uint crc2 = CRC32custom.Default.ComputeHash(bytes);
            if (crc != crc2)
                return new string[2];
            return Encoding.UTF8.GetString(bytes).Split('$');
        }
        */
        string GetOuterStackTrace(int skipFrames) {
            if (stackTraceHelper != null)
                return stackTraceHelper.GetOuterStackTrace(skipFrames + 1); // remove GetOuterStackTrace call from stack trace
            else
                return String.Empty;
        }
        string GetOuterNormalizedStackTrace(int skipFrames) {
            if (stackTraceHelper != null)
                return stackTraceHelper.GetOuterNormalizedStackTrace(skipFrames + 1); // remove GetOuterNormalizedStackTrace call from stack trace
            else
                return String.Empty;
        }
        void AppendOuterStack(Exception ex, int skipFrames) {
            try {
                if (ex != null && ex.Data != null) {
                    ex.Data[OuterStackKeys.Stack] = GetOuterStackTrace(skipFrames + 1); // remove AppendOuterStack from stacktrace
                    ex.Data[OuterStackKeys.StackNormalized] = GetOuterNormalizedStackTrace(skipFrames + 1); // remove AppendOuterStack from stacktrace
                }
            }
            catch {
            }
        }
        void RemoveOuterStack(Exception ex) {
            try {
                if (ex != null && ex.Data != null) {
                    if (ex.Data.Contains(OuterStackKeys.Stack))
                        ex.Data.Remove(OuterStackKeys.Stack);
                    if (ex.Data.Contains(OuterStackKeys.StackNormalized))
                        ex.Data.Remove(OuterStackKeys.StackNormalized);
                }
            }
            catch {
            }
        }

        const int skipFramesForAppendOuterStackRootMethod = 2; // remove from stacktrace Send call and calling method
        public void Send(Exception ex) {
            AppendOuterStack(ex, skipFramesForAppendOuterStackRootMethod);
            try {
                ReportException(ex, null, null);
            }
            finally {
                RemoveOuterStack(ex);
            }
        }
        public void Send(Exception ex, IDictionary<string, string> additionalCustomData) {
            AppendOuterStack(ex, skipFramesForAppendOuterStackRootMethod); // remove from stacktrace Send call and calling method
            try {
                ReportException(ex, additionalCustomData, null);
            }
            finally {
                RemoveOuterStack(ex);
            }
        }
        public void Send(Exception ex, IDictionary<string, string> additionalCustomData, AttachmentCollection additionalAttachments) {
            AppendOuterStack(ex, skipFramesForAppendOuterStackRootMethod); // remove from stacktrace Send call and calling method
            try {
                ReportException(ex, additionalCustomData, additionalAttachments);
            }
            finally {
                RemoveOuterStack(ex);
            }
        }
#if ALLOW_ASYNC
        public async Task<bool> SendAsync(Exception ex) {
            AppendOuterStack(ex, skipFramesForAppendOuterStackRootMethod); // remove from stacktrace Send call and calling method
            try {
                return await ReportExceptionAsync(ex, null, null);
            }
            finally {
                RemoveOuterStack(ex);
            }
        }
        public async Task<bool> SendAsync(Exception ex, IDictionary<string, string> additionalCustomData) {
            AppendOuterStack(ex, skipFramesForAppendOuterStackRootMethod); // remove from stacktrace Send call and calling method
            try {
                return await ReportExceptionAsync(ex, additionalCustomData, null);
            }
            finally {
                RemoveOuterStack(ex);
            }
        }
        public async Task<bool> SendAsync(Exception ex, IDictionary<string, string> additionalCustomData, AttachmentCollection additionalAttachments) {
            AppendOuterStack(ex, skipFramesForAppendOuterStackRootMethod); // remove from stacktrace Send call and calling method
            try {
                return await ReportExceptionAsync(ex, additionalCustomData, additionalAttachments);
            }
            finally {
                RemoveOuterStack(ex);
            }
        }
#endif
        protected bool ReportException(Exception ex, IDictionary<string, string> additionalCustomData, AttachmentCollection additionalAttachments) {
            try {
                if (!RaiseCanReportException(ex))
                    return false;

                if (ExceptionLoggerFactory.Instance.PlatformIgnoreDetection != null &&
                    ExceptionLoggerFactory.Instance.PlatformIgnoreDetection.ShouldIgnoreException(ex) == ShouldIgnoreResult.Ignore)
                    return false;

                RaiseBeforeReportException(ex);

                bool success = ExceptionLogger.ReportException(ex, CreateDefaultCollector(additionalCustomData, additionalAttachments));
                RaiseAfterReportException(ex);
                return success;
            } catch {
                return false;
            }
        }
#if ALLOW_ASYNC
        protected async Task<bool> ReportExceptionAsync(Exception ex, IDictionary<string, string> additionalCustomData, AttachmentCollection additionalAttachments) {
            try {
                if (!RaiseCanReportException(ex))
                    return false;

                if (ExceptionLoggerFactory.Instance.PlatformIgnoreDetection != null &&
                    ExceptionLoggerFactory.Instance.PlatformIgnoreDetection.ShouldIgnoreException(ex) == ShouldIgnoreResult.Ignore)
                    return false;

                RaiseBeforeReportException(ex);

                bool success = await ExceptionLogger.ReportExceptionAsync(ex, CreateDefaultCollector(additionalCustomData, additionalAttachments));
                RaiseAfterReportException(ex);
                return success;
            } catch {
                return false;
            }
        }
#endif
    }



    public delegate void CanReportExceptionEventHandler(object sender, CanReportExceptionEventArgs args);
    public class CanReportExceptionEventArgs : CancelEventArgs {
        public Exception Exception { get; internal set; }
    }

    public delegate void BeforeReportExceptionEventHandler(object sender, BeforeReportExceptionEventArgs args);
    public class BeforeReportExceptionEventArgs : EventArgs {
        public Exception Exception { get; internal set; }
    }

    public delegate void AfterReportExceptionEventHandler(object sender, AfterReportExceptionEventArgs args);
    public class AfterReportExceptionEventArgs : EventArgs {
        public Exception Exception { get; internal set; }
    }

    [AttributeUsage(AttributeTargets.All)]
    public class LogifyIgnoreAttribute : Attribute {

        public LogifyIgnoreAttribute() : this(true) {
        }
        public LogifyIgnoreAttribute(bool ignore) {
            this.Ignore = ignore;
        }

        public bool Ignore { get; set; }
    }
}

namespace DevExpress.Logify.Core.Internal {
    public enum ShouldIgnoreResult {
        Unknown,
        Ignore,
        Process
    }

    public interface IExceptionIgnoreDetection {
        ShouldIgnoreResult ShouldIgnoreException(Exception ex);
    }

    public static class LogifyClientAccessor {
        public static ReportConfirmationModel CreateConfirmationModel(LogifyClientExceptionReport report, Func<LogifyClientExceptionReport, bool> sendAction) {
            if (LogifyClientBase.Instance != null)
                return LogifyClientBase.Instance.CreateConfirmationModel(report, sendAction);
            else
                return null;
        }
        public static bool RaiseConfirmationDialogShowing(ReportConfirmationModel model) {
            if (LogifyClientBase.Instance != null)
                return LogifyClientBase.Instance.RaiseConfirmationDialogShowing(model);
            else
                return false;
        }

        public static void AfterConfigure(LogifyClientBase client) {
            client.ForceUpdateBreadcrumbsMaxCount();
            client.InitAfterConfigure();
        }
    }

    public interface IStackTraceHelper {
        string GetOuterStackTrace(int skipFrames);
        string GetOuterNormalizedStackTrace(int skipFrames);
    }
    public static class OuterStackKeys {
        public const string Stack = "#logify_outer_stack";
        public const string StackNormalized = "#logify_outer_stack_normalized";
    }
}