﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using Heibroch.Common;
using Heibroch.Common.Wpf;
using Heibroch.Launch.Events;
using Heibroch.Launch.Plugin;
using Heibroch.Launch.Views;

namespace Heibroch.Launch.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IEventBus eventBus;

        private readonly IShortcutCollection<string, ILaunchShortcut> shortcutCollection;
        private readonly IShortcutExecutor shortcutExecutor;
        private readonly ISettingCollection settingCollection;
        private readonly IPluginLoader pluginLoader;
        private static ShortcutWindow currentShortcutWindow = null;
        private static ShortcutViewModel shortcutViewModel;
        private static SettingsWindow currentSettingsWindow = null;
        private static SettingsViewModel settingsViewModel;
        private static ArgumentsWindow currentArgumentsWindow = null;
        private static ArgumentsViewModel argumentsViewModel;


        private DispatcherTimer dispatcherTimer;
        private TrayIcon trayIcon;

        public MainViewModel() : this(Container.Current.Resolve<IEventBus>(),
                                      Container.Current.Resolve<IShortcutCollection<string, ILaunchShortcut>>(),
                                      Container.Current.Resolve<IShortcutExecutor>(),
                                      Container.Current.Resolve<ISettingCollection>(),
                                      Container.Current.Resolve<IPluginLoader>())
        {

        }

        public MainViewModel(IEventBus eventBus,
                             IShortcutCollection<string, ILaunchShortcut> shortcutCollection,
                             IShortcutExecutor shortcutExecutor,
                             ISettingCollection settingCollection,
                             IPluginLoader pluginLoader)
        {
            this.eventBus = eventBus;
            this.shortcutCollection = shortcutCollection;
            this.shortcutExecutor = shortcutExecutor;
            this.settingCollection = settingCollection;
            this.pluginLoader = pluginLoader;
            Initialize();
        }

        private void Initialize()
        {
            shortcutViewModel = new ShortcutViewModel(shortcutCollection, shortcutExecutor, pluginLoader);
            settingsViewModel = new SettingsViewModel(settingCollection);
            argumentsViewModel = new ArgumentsViewModel(shortcutExecutor);

            eventBus.Subscribe<GlobalKeyPressed>(OnKeyPressed);

            dispatcherTimer = new DispatcherTimer(DispatcherPriority.Background);
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(100);
            dispatcherTimer.Tick += OnDispatcherTimerTick;
            dispatcherTimer.Start();

            trayIcon = new TrayIcon(OnTrayIconContextMenuItemClicked, new List<string> { "Settings", "Exit" });
        }

        private void OnTrayIconContextMenuItemClicked(string obj)
        {
            if (obj == Constants.ContextMenu.Exit)
            {
                trayIcon.Dispose();
                System.Windows.Application.Current.Shutdown();
            }

            if (obj == Constants.ContextMenu.Settings)
            {
                currentSettingsWindow = new SettingsWindow();
                currentSettingsWindow.DataContext = settingsViewModel;
                currentSettingsWindow.Show();
                currentSettingsWindow.Activate();
            }
        }

        private void OnDispatcherTimerTick(object sender, EventArgs e)
        {
            if (currentShortcutWindow == null) return;

            if (!currentShortcutWindow.IsActive)
                currentShortcutWindow.Activate();

            //if (!currentShortcutWindow.IsFocused)
            //    currentShortcutWindow.Focus();
        }

        private void OnKeyPressed(GlobalKeyPressed obj)
        {
            if (currentShortcutWindow != null || currentArgumentsWindow != null)
            {
                switch (obj.Key)
                {
                    case 0x0000001B: //Escape
                        obj.ProcessKey = false;
                        CloseShortcutWindow();
                        CloseArgumentWindow();
                        break;
                    case 0x00000028: //Down
                        shortcutViewModel?.IncrementSelection(1);
                        break;
                    case 0x00000026: //Up
                        shortcutViewModel?.IncrementSelection(-1);
                        break;
                    case 0x0000000D: //Enter
                        obj.ProcessKey = false;

                        //If the current shortcut window is open and doesn't have args, then execute it
                        if (currentShortcutWindow != null && !IsArgumentShortcut())
                        {
                            shortcutViewModel.ExecuteSelection();
                            CloseShortcutWindow();
                            break;
                        }

                        //If the arg window is open and it's not filled out then open the arg window
                        if (currentArgumentsWindow != null && IsArgumentShortcut() && !IsArgumentShortcutFilled())
                        {
                            argumentsViewModel.ExecuteArgument();

                            var command = argumentsViewModel.Command;
                            var arguments = GetArgs();

                            CloseArgumentWindow();

                            if (arguments.Count() <= shortcutExecutor.Arguments.Count)
                            {
                                shortcutExecutor.Execute(command.Title, command);
                                break;
                            }

                            var argumentKey = arguments.ElementAt(shortcutExecutor.Arguments.Count);

                            argumentsViewModel.Command = command;
                            argumentsViewModel.ArgumentKey = argumentKey;

                            if (IsArgumentShortcutFilled())
                                break;

                            OpenArgumentWindow();
                        }

                        //If the shortcut window is open and it has args, then open the arg window
                        if (currentShortcutWindow != null && IsArgumentShortcut() && !IsArgumentShortcutFilled())
                        {
                            var command = shortcutViewModel.SelectedItem.Value;
                            var argumentKey = GetArgs().ElementAt(shortcutExecutor.Arguments.Count);

                            CloseShortcutWindow();

                            argumentsViewModel.Command = command;
                            argumentsViewModel.ArgumentKey = argumentKey;
                           
                            OpenArgumentWindow();
                            break;
                        }

                        //if the arg window is open and it's filled out, then execute the shortcut
                        if (currentArgumentsWindow != null && IsArgumentShortcut() && IsArgumentShortcutFilled())
                        {
                            argumentsViewModel.Execute();
                            CloseArgumentWindow();
                            break;
                        }

                        break;
                }
            }
            else if (Keyboard.Modifiers == (settingsViewModel.Modifier1 | settingsViewModel.Modifier2) && obj.Key == (int)settingsViewModel.Key) //Space
            {
                currentShortcutWindow = new ShortcutWindow();
                currentShortcutWindow.DataContext = shortcutViewModel;
                currentShortcutWindow.Show();
                currentShortcutWindow.Activate();

                obj.ProcessKey = false;

                shortcutViewModel.Reset();
            }
        }

        private bool IsArgumentShortcut()
        {
            var description = shortcutViewModel?.SelectedItem.Value?.Description ?? argumentsViewModel?.Command?.Description;
            if (description == null) return false;
            return shortcutExecutor.IsArgShortcut(description);
        }

        private bool IsArgumentShortcutFilled() => shortcutExecutor.Arguments.Count >= GetArgs().Count();

        private IEnumerable<string> GetArgs()
        {
            var description = shortcutViewModel?.SelectedItem.Value?.Description ?? argumentsViewModel?.Command?.Description;
            if (description == null) return new List<string>();
            return shortcutExecutor.GetArgKeys(description);
        }

        private void CloseShortcutWindow()
        {
            shortcutViewModel.Reset();
            currentShortcutWindow?.Close();
            currentShortcutWindow = null;
        }

        private void OpenArgumentWindow()
        {
            currentArgumentsWindow = new ArgumentsWindow();
            currentArgumentsWindow.DataContext = argumentsViewModel;
            currentArgumentsWindow.Show();
            currentArgumentsWindow.Activate();
        }

        private void CloseArgumentWindow()
        {
            argumentsViewModel.Reset();
            currentArgumentsWindow?.Close();
            currentArgumentsWindow = null;
        }
    }
}