﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Heibroch.Common;
using Heibroch.Common.Wpf;
using Heibroch.Launch.Plugin;

namespace Heibroch.Launch.ViewModels
{
    public class ShortcutViewModel : ViewModelBase
    {
        private readonly IShortcutCollection<string, ILaunchShortcut> shortcutCollection;
        private readonly IShortcutExecutor shortcutExecutor;
        private readonly IEventBus eventBus;
        private readonly SelectionCycler selectionCycler;
        private KeyValuePair<string, ILaunchShortcut> selectedItem;
        
        public ShortcutViewModel(IShortcutCollection<string, ILaunchShortcut> shortcutCollection, 
                                 IShortcutExecutor shortcutExecutor,
                                 IEventBus eventBus)
        {
            this.shortcutCollection = shortcutCollection;
            this.shortcutExecutor = shortcutExecutor;
            this.eventBus = eventBus;

            selectionCycler = new SelectionCycler();

            Initialize();
        }

        private void Initialize()
        {
            shortcutCollection.Load();
            eventBus.Publish(new ShortcutsLoadedEvent());
        }
        
        public string LaunchText
        {
            get => shortcutCollection.CurrentQuery ?? string.Empty;
            set
            {
                var oldValue = shortcutCollection?.CurrentQuery ?? string.Empty;

                shortcutCollection.Filter(value);

                if (value.Length < oldValue.Length && oldValue.StartsWith(value))
                    selectionCycler.Reset();

                RaisePropertyChanged(nameof(LaunchText));
                RaisePropertyChanged(nameof(QueryResults));
                RaisePropertyChanged(nameof(DisplayedQueryResults));
                RaisePropertyChanged(nameof(QueryResultsVisibility));
                RaisePropertyChanged(nameof(WaterMarkVisibility));

                if (!QueryResults.ContainsKey(selectedItem.Key ?? string.Empty))
                    SelectedItem = QueryResults.FirstOrDefault();

                RaisePropertyChanged(nameof(SelectedItem));
            }
        }
        
        public SortedList<string, ILaunchShortcut> DisplayedQueryResults => new SortedList<string, ILaunchShortcut>(selectionCycler.SubSelect(QueryResults).ToDictionary(x => x.Key, x => x.Value));

        public SortedList<string, ILaunchShortcut> QueryResults => shortcutCollection.QueryResults;

        public KeyValuePair<string, ILaunchShortcut> SelectedItem
        {
            get => selectedItem;
            set
            {
                selectedItem = value;
                RaisePropertyChanged(nameof(SelectedItem));
            }
        }

        public void Reset()
        {
            LaunchText = string.Empty;
            selectionCycler.Reset();
        }

        public void IncrementSelection(int increment)
        {
            if (DisplayedQueryResults.Count <= 0) return;

            //Move/cycle visibility/shortcuts
            selectionCycler.Increment(increment, shortcutCollection.QueryResults.Count);

            if (string.IsNullOrWhiteSpace(SelectedItem.Key))
            {
                SelectedItem = DisplayedQueryResults.First();
                return;
            }

            if (!DisplayedQueryResults.ContainsKey(SelectedItem.Key) && DisplayedQueryResults.Any())
            {
                SelectedItem = DisplayedQueryResults.First();
                return;
            }

            if (DisplayedQueryResults.Count == 1)
            {
                SelectedItem = DisplayedQueryResults.First();
                return;
            }

            var index = DisplayedQueryResults.IndexOfKey(SelectedItem.Key) + increment;
            if (index >= DisplayedQueryResults.Count || index < 0) return;

            RaisePropertyChanged(nameof(DisplayedQueryResults));

            SelectedItem = DisplayedQueryResults.ElementAt(selectionCycler.CycleIndex);

            Debug.WriteLine($"SelectedItem now {SelectedItem.Key} \r\n" +
                            $"StartIndex {selectionCycler.StartIndex}\r\n" +
                            $"StopIndex {selectionCycler.StopIndex}\r\n" +
                            $"TotalIndex {selectionCycler.CurrentIndex}\r\n" +
                            $"CycleIndex {selectionCycler.CycleIndex}\r\n" +
                            $"Delta {selectionCycler.Delta}\r\n" +
                            $"CollectionCount {shortcutCollection.QueryResults.Count}" +
                            $"-----------------------------------------------");
        }
        
        public void ExecuteSelection() => shortcutExecutor.Execute(QueryResults.Count == 1 ? QueryResults.First().Key : (SelectedItem.Key ?? LaunchText));

        public Visibility QueryResultsVisibility => QueryResults.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility WaterMarkVisibility => LaunchText?.Length <= 0 ? Visibility.Visible : Visibility.Hidden;
    }
}
