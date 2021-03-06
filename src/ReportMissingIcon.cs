﻿using System;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace FileIcons
{
    internal sealed class ReportMissingIcon
    {
        private const string _urlFormat = "https://github.com/madskristensen/FileIcons/issues/new?title={0}&body={1}";

        private readonly Package _package;
        private string[] _shellExtensions;
        private string _ext;

        private ReportMissingIcon(Package package)
        {
            _package = package;

            var commandService = (OleMenuCommandService)ServiceProvider.GetService(typeof(IMenuCommandService));
            if (commandService != null)
            {
                var id = new CommandID(PackageGuids.guidVSPackageCmdSet, PackageIds.ReportMissingIconId);
                var command = new OleMenuCommand(Execute, id);
                command.BeforeQueryStatus += MenuItem_BeforeQueryStatus;
                commandService.AddCommand(command);
            }
        }

        public static ReportMissingIcon Instance { get; private set; }

        private IServiceProvider ServiceProvider
        {
            get { return _package; }
        }

        public static void Initialize(Package package)
        {
            Instance = new ReportMissingIcon(package);
        }
        private void MenuItem_BeforeQueryStatus(object sender, EventArgs e)
        {
            var button = (OleMenuCommand)sender;
            button.Enabled = button.Visible = false;

            try
            {
                var item = GetSelectedItem() as ProjectItem;

                if (item == null || item.FileCount == 0)
                    return;

                _ext = Path.GetExtension(item.FileNames[1]);
                bool isIconMissing = IsIconMissing(_ext);

                button.Text = $"Report missing icon for {_ext} files...";
                button.Enabled = button.Visible = isIconMissing;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex);
            }
        }

        private void Execute(object sender, EventArgs e)
        {
            string title = Uri.EscapeUriString($"Missing icon for {_ext} files");
            string body = Uri.EscapeUriString("Optionally, please give more details as to what the file type is used for.");
            string url = string.Format(_urlFormat, title, body);

            System.Diagnostics.Process.Start(url);
        }

        private bool IsIconMissing(string fileExtension)
        {
            // Icons can't be associated with extensionless files
            if (string.IsNullOrWhiteSpace(fileExtension))
                return false;

            if (_shellExtensions == null)
            {
                using (var key = _package.ApplicationRegistryRoot.OpenSubKey("ShellFileAssociations"))
                {
                    _shellExtensions = key.GetSubKeyNames();
                }
            }

            return !_shellExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase);
        }

        public static object GetSelectedItem()
        {
            IntPtr hierarchyPointer, selectionContainerPointer;
            object selectedObject = null;
            IVsMultiItemSelect multiItemSelect;
            uint itemId;

            var monitorSelection = (IVsMonitorSelection)Package.GetGlobalService(typeof(SVsShellMonitorSelection));

            try
            {
                monitorSelection.GetCurrentSelection(out hierarchyPointer,
                                                 out itemId,
                                                 out multiItemSelect,
                                                 out selectionContainerPointer);

                IVsHierarchy selectedHierarchy = Marshal.GetTypedObjectForIUnknown(
                                                     hierarchyPointer,
                                                     typeof(IVsHierarchy)) as IVsHierarchy;

                if (selectedHierarchy != null)
                {
                    ErrorHandler.ThrowOnFailure(selectedHierarchy.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_ExtObject, out selectedObject));
                }

                Marshal.Release(hierarchyPointer);
                Marshal.Release(selectionContainerPointer);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex);
            }

            return selectedObject;
        }
    }
}
