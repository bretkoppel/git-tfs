using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Sep.Git.Tfs.Core.TfsInterop;
using Sep.Git.Tfs.Util;
using Sep.Git.Tfs.VsCommon;
using StructureMap;

namespace Sep.Git.Tfs.Vs2008
{
    public class TfsHelper : TfsHelperBase
    {
        private TeamFoundationServer _server;

        public TfsHelper(TextWriter stdout, TfsApiBridge bridge, IContainer container) : base(stdout, bridge, container)
        {
        }

        public override string TfsClientLibraryVersion
        {
            get { return "" + typeof (TeamFoundationServer).Assembly.GetName().Version + " (MS)"; }
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
                    new TeamFoundationServer(Url, GetCredential(), new UICredentialsProvider()) :
                    new TeamFoundationServer(Url, new UICredentialsProvider());

                _server.EnsureAuthenticated();
            }
        }

        protected override T GetService<T>()
        {
            return (T) _server.GetService(typeof (T));
        }

        protected override string GetAuthenticatedUser()
        {
            return VersionControl.AuthenticatedUser;
        }

        public override bool CanShowCheckinDialog
        {
            get { return false; }
        }

        public override bool CanPerformGatedCheckin
        {
            get { return false; }
        }

        public override long ShowCheckinDialog(IWorkspace workspace, IPendingChange[] pendingChanges, IEnumerable<IWorkItemCheckedInfo> checkedInfos, string checkinComment)
        {
            throw new NotImplementedException();
        }

        public override int CheckIn(IWorkspace workspace, IPendingChange[] changes, string comment, ICheckinNote checkinNote, IEnumerable<IWorkItemCheckinInfo> workItemChanges, TfsPolicyOverrideInfo policyOverrideInfo, bool queueGatedCheckIn)
        {
            var tfsWorkspace = _bridge.Unwrap<Workspace>(workspace);
            return tfsWorkspace.CheckIn(
                _bridge.Unwrap<PendingChange>(changes),
                comment,
                _bridge.Unwrap<CheckinNote>(checkinNote),
                _bridge.Unwrap<WorkItemCheckinInfo>(workItemChanges),
                WrapperForWorkspace.ToTfs(policyOverrideInfo, _bridge));
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
            var tempfile = new TemporaryFile();
            _bridge.Unwrap<Item>(item).DownloadFile(tempfile);
            return tempfile.ToStream();
        }
    }
}
