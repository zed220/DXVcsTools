﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using DXVcsTools.Core;
using DXVcsTools.DXVcsClient;
using DXVcsTools.UI;
using DevExpress.Xpf.Mvvm;
using DevExpress.Xpf.Mvvm.Native;
using EnvDTE;

namespace DXVcsTools.VSIX {
    public class ToolWindowViewModel : BindableBase, IUpdatableViewModel, ISupportServices {
        readonly DTE dte;
        SolutionItem solutionItem;
        ProjectItemBase selectedItem;
        DXVcsBranch currentBranch;
        DXVcsBranch masterBranch;
        bool canTotalMerge;
        IEnumerable flatSource;
        double mergeProgress;

        public double MergeProgress {
            get { return mergeProgress; }
            set { SetProperty(ref mergeProgress, value, "MergeProgress"); }
        }
        public bool CanTotalMerge {
            get { return canTotalMerge; }
            set { SetProperty(ref canTotalMerge, value, "CanTotalMerge", CommandManager.InvalidateRequerySuggested); }
        }
        public DXVcsBranch MasterBranch {
            get { return masterBranch; }
            private set { SetProperty(ref masterBranch, value, "MasterBranch", CommandManager.InvalidateRequerySuggested); }
        }
        public DXVcsBranch CurrentBranch {
            get { return currentBranch; }
            set { SetProperty(ref currentBranch, value, "CurrentBranch", CurrentBranchChanged); }
        }
        public IEnumerable<DXVcsBranch> AvailableBranches { get { return Options.Branches; } }
        public SolutionItem Solution {
            get { return solutionItem; }
            set { SetProperty(ref solutionItem, value, "Solution"); }
        }
        public IEnumerable Source {
            get { return flatSource; }
            private set { SetProperty(ref flatSource, value, "Source", CommandManager.InvalidateRequerySuggested); }
        }
        public ProjectItemBase SelectedItem {
            get { return selectedItem; }
            set { SetProperty(ref selectedItem, value, "SelectedItem", CommandManager.InvalidateRequerySuggested); }
        }
        PortOptionsViewModel PortOptions { get; set; }
        public OptionsViewModel Options { get; private set; }

        public RelayCommand<bool?> MergeCommand { get; private set; }
        public RelayCommand MergeAllCommand { get; private set; }
        public RelayCommand BlameCommand { get; private set; }
        public DelegateCommand UpdateCommand { get; private set; }
        public RelayCommand CheckInCommand { get; private set; }

        DXVcsBranch FindMasterBranch(PortOptionsViewModel portOptions) {
            string relativePath = portOptions.GetRelativePath(Solution.Path);
            return Options.Branches.FirstOrDefault(branch => relativePath.StartsWith(branch.Path));
        }
        public ToolWindowViewModel(DTE dte, OptionsViewModel options) {
            this.dte = dte;
            Options = options;
            ServiceContainer = new ServiceContainer(this);

            MergeCommand = new RelayCommand<bool?>(Merge, CanMerge);
            MergeAllCommand = new RelayCommand(MergeAll, CanMergeAll);
            UpdateCommand = new DelegateCommand(Update, CanUpdate);
            BlameCommand = new RelayCommand(Blame, CanBlame);
            CheckInCommand = new RelayCommand(CheckIn, CanCheckIn);
        }
        void Merge(bool? parameter) {
            bool showPreview = parameter.HasValue ? parameter.Value : Options.ReviewTarget;
            SelectedItem.MergeState = PerformMerge(SelectedItem, showPreview);
        }
        bool CanMerge(bool? parameter) {
            return SelectedItem.Return(x => x.IsCheckOut && x.MergeState == MergeState.None, () => false);
        }
        MergeState PerformMerge(ProjectItemBase item, bool showPreview) {
            MergeHelper helper = new MergeHelper(Options, PortOptions);
            return helper.MergeChanges(CurrentBranch, item.Path, null, showPreview);
        }
        void MergeAll() {
            List<ProjectItemBase> items = Source.Cast<ProjectItemBase>().Where(item => item.MergeState == MergeState.None).ToList();
            foreach (ProjectItemBase item in items) {
                item.MergeState = PerformMerge(item, false);
            }
        }
        bool CanMergeAll() {
            return true;
        }
        public void Update() {
            DteWrapper dteWrapper = new DteWrapper(dte);
            Solution = dteWrapper.BuildTree();
            var source = new ListCollectionView(GetFlatItemsSource().Cast<object>().ToList());
            source.Filter = item => (item as ProjectItemBase).Return(x => x.IsCheckOut, () => false);
            Source = source;

            PortOptions = new PortOptionsViewModel(Solution.Path, Options);
            MasterBranch = FindMasterBranch(PortOptions);
            CanTotalMerge = MasterBranch != null;
            PortOptions.MasterBranch = MasterBranch;

            CurrentBranch = Options.Branches.LastOrDefault(item => item != MasterBranch);
            MergeProgress = 0;
        }
        bool CanUpdate() {
            return true;
        }
        IEnumerable GetFlatItemsSource() {
            if (Solution == null)
                return new List<object>();
            return GetChildren(Solution);
        }
        IEnumerable<ProjectItemBase> GetChildren(ProjectItemBase root) {
            if (root.Children == null)
                yield break;
            foreach (var item in root.Children) {
                if (item is FileItem)
                    yield return item;
                foreach (var subItem in GetChildren(item)) {
                    if (subItem is FileItem)
                        yield return subItem;
                }
            }
        }
        void CurrentBranchChanged() {
            CommandManager.InvalidateRequerySuggested();
            Update();
        }
        void Blame() {
            MergeHelper helper = new MergeHelper(Options, PortOptions);
        }
        bool CanBlame() {
            return SelectedItem != null;
        }
        bool CanCheckIn() {
            return SelectedItem.If(x => x.IsCheckOut).ReturnSuccess();
        }
        void CheckIn() {
            CheckInViewModel model = new CheckInViewModel(SelectedItem.Path, false);
            bool? result = GetService<IDialogService>().ShowDialog("CheckInControl");
            if (result != null && (bool)result) {
                MergeHelper helper = new MergeHelper(Options, PortOptions);
                helper.CheckIn(model);
                SelectedItem.IsChecked = model.StaysChecked;
            }
        }

        public IServiceContainer ServiceContainer { get; private set; }
        protected virtual T GetService<T>(ServiceSearchMode searchMode = ServiceSearchMode.PreferLocal) where T : class {
            return ServiceContainer.GetService<T>(searchMode);
        }
    }
}