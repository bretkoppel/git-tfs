using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.Win32;
using Sep.Git.Tfs.Core.TfsInterop;
using Sep.Git.Tfs.Util;
using Sep.Git.Tfs.VsCommon;
using StructureMap;

namespace Sep.Git.Tfs.Vs2010
{
    using System.Net;

    using Microsoft.TeamFoundation.Framework.Client;

    public class TfsHelper : TfsHelperBase
    {
        private readonly TfsApiBridge _bridge;
        private TfsTeamProjectCollection _server;

        public TfsHelper(TextWriter stdout, TfsApiBridge bridge, IContainer container) : base(stdout, bridge, container)
        {
            _bridge = bridge;
        }

        public override string TfsClientLibraryVersion
        {
            get { return typeof(TfsTeamProjectCollection).Assembly.GetName().Version + " (MS)"; }
        }

        public override void EnsureAuthenticated()
        {
            if (string.IsNullOrEmpty(Url))
            {
                _server = null;
            }
            else
            {
                _server = HasCredentials ?
                    new TfsTeamProjectCollection(new Uri(Url), GetCredential(), new UICredentialsProvider()) :
                    new TfsTeamProjectCollection(new Uri(Url), new UICredentialsProvider());

                _server.EnsureAuthenticated();
            }
        }

        protected override T GetService<T>()
        {
            return (T) _server.GetService(typeof (T));
        }

        protected override string GetAuthenticatedUser()
        {
            return VersionControl.TeamProjectCollection.AuthorizedIdentity.DisplayName;
        }

        public override bool CanShowCheckinDialog { get { return true; } }

        public override bool CanPerformGatedCheckin
        {
            get { return true; }
        }

        public override int CheckIn(IWorkspace workspace, IPendingChange[] changes, string comment, ICheckinNote checkinNote, IEnumerable<IWorkItemCheckinInfo> workItemChanges, TfsPolicyOverrideInfo policyOverrideInfo, bool queueGatedCheckIn)
        {
            var tfsWorkspace = _bridge.Unwrap<Workspace>(workspace);
            WorkspaceCheckInParameters parms = new WorkspaceCheckInParameters(_bridge.Unwrap<PendingChange>(changes), comment);
            parms.CheckinNotes = _bridge.Unwrap<CheckinNote>(checkinNote);
            parms.AssociatedWorkItems = _bridge.Unwrap<WorkItemCheckinInfo>(workItemChanges);
            parms.PolicyOverride = WrapperForWorkspace.ToTfs(policyOverrideInfo, _bridge);
            parms.OverrideGatedCheckIn = queueGatedCheckIn;
            return tfsWorkspace.CheckIn(parms);
        }

        public override long ShowCheckinDialog(IWorkspace workspace, IPendingChange[] pendingChanges, IEnumerable<IWorkItemCheckedInfo> checkedInfos, string checkinComment)
        {
            return ShowCheckinDialog(_bridge.Unwrap<Workspace>(workspace),
                                     pendingChanges.Select(p => _bridge.Unwrap<PendingChange>(p)).ToArray(),
                                     checkedInfos.Select(c => _bridge.Unwrap<WorkItemCheckedInfo>(c)).ToArray(),
                                     checkinComment);
        }

        private long ShowCheckinDialog(Workspace workspace, PendingChange[] pendingChanges, 
            WorkItemCheckedInfo[] checkedInfos, string checkinComment)
        {
            using (var parentForm = new ParentForm())
            {
                parentForm.Show();

                var dialog = Activator.CreateInstance(GetCheckinDialogType(), new object[] {workspace.VersionControlServer});

                return dialog.Call<int>("Show", parentForm.Handle, workspace, pendingChanges, pendingChanges,
                                        checkinComment, null, null, checkedInfos);
            }
        }

        private const string DialogAssemblyName = "Microsoft.TeamFoundation.VersionControl.ControlAdapter";

        private static Type GetCheckinDialogType()
        {
            return GetDialogAssembly().GetType(DialogAssemblyName + ".CheckinDialog");
        }

        private static Assembly GetDialogAssembly()
        {
            return Assembly.LoadFrom(GetDialogAssemblyPath());
        }

        private static string GetDialogAssemblyPath()
        {
            return Path.Combine(GetVs2010InstallDir(), "PrivateAssemblies", DialogAssemblyName + ".dll");
        }

        private static string GetVs2010InstallDir()
        {
            return TryGetRegString(@"Software\Microsoft\VisualStudio\10.0", "InstallDir")
                ?? TryGetRegString(@"Software\WOW6432Node\Microsoft\VisualStudio\10.0", "InstallDir");
        }

        private static string TryGetRegString(string path, string name)
        {
            try
            {
                Trace.WriteLine("Trying to get " + path + "|" + name);
                var key = Registry.LocalMachine.OpenSubKey(path);
                if(key != null)
                {
                    return key.GetValue(name) as string;
                }
            }
            catch(Exception e)
            {
                Trace.WriteLine("Unable to get registry value " + path + "|" + name + ": " + e);
            }
            return null;
        }
    }

    public class ItemDownloadStrategy : IItemDownloadStrategy
    {
        private readonly TfsApiBridge _bridge;

        public ItemDownloadStrategy(TfsApiBridge bridge)
        {
            _bridge = bridge;
        }

        public Stream DownloadFile(IItem item)
        {
            return _bridge.Unwrap<Item>(item).DownloadFile();
        }
    }
}
