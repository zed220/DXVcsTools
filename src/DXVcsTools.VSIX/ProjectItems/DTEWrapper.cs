﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevExpress.Xpf.Mvvm.Native;
using EnvDTE;

namespace DXVcsTools.Core {
    public class DteWrapper : IDteWrapper {
        DTE dte;
        public DteWrapper(DTE dte) {
            this.dte = dte;
        }
        public SolutionItem BuildTree() {
            return new SolutionItem(GetProjects(dte.Solution)) { Name = dte.Solution.FullName };
        }
        IEnumerable<ProjectItem> GetProjects(Solution solution) {
            string name = solution.FullName;
            return solution.Projects.Cast<Project>().Select(item => new ProjectItem(GetFilesAndDirectories(item)) { Name = name });
        }
        IEnumerable<FileItemBase> GetFilesAndDirectories(Project project) {
            var children = project.ProjectItems;
            if (children.If(x => x.Count == 0).ReturnSuccess())
                yield break;
            foreach (EnvDTE.ProjectItem projectItem in children) {
                var item = GetItem(projectItem);
                if (item != null)
                    yield return item;
            }
        }
        FileItemBase GetItem(EnvDTE.ProjectItem projectItem) {
            if (projectItem.FileCount < 1)
                return null;
            string fileName = projectItem.FileNames[0];
            FileItemBase item = null;
            if (File.Exists(fileName)) {
                var fileInfo = new FileInfo(fileName);
                item = new FileItem() { Name = fileInfo.Name };
            }
            if (Directory.Exists(fileName)) {
                DirectoryInfo info = new DirectoryInfo(fileName);
                item = new FolderItem(GetChildrenItems(projectItem)) { Name = info.Name, Path = fileName };
            }
            if (item == null)
                return null;
            item.IsChecked = dte.SourceControl.IsItemCheckedOut(projectItem.Name);
            return item;
        }
        IEnumerable<FileItemBase> GetChildrenItems(EnvDTE.ProjectItem projectItem) {
            foreach (EnvDTE.ProjectItem childItem in projectItem.ProjectItems) {
                var item = GetItem(childItem);
                if (item != null)
                    yield return item;
            }
        }
    }
}
