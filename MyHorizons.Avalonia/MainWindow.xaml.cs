﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MyHorizons.Avalonia.Controls;
using MyHorizons.Avalonia.Utility;
using MyHorizons.Data;
using MyHorizons.Data.PlayerData;
using MyHorizons.Data.Save;
using MyHorizons.Data.TownData;
using MyHorizons.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MyHorizons.Avalonia
{
    public class MainWindow : Window
    {
        private static MainWindow? _singleton;

        private MainSaveFile? saveFile;
        private Player? selectedPlayer;
        private Villager? selectedVillager;

        private Grid TitleBarGrid;

        private Grid CloseGrid;
        private Button CloseButton;

        private Grid ResizeGrid;
        private Button ResizeButton;

        private Grid MinimizeGrid;
        private Button MinimizeButton;

        private bool playerLoading = false;
        private bool settingItem = false;
        private Dictionary<ushort, string>? itemDatabase;
        private Dictionary<byte, string>[]? villagerDatabase;

        private readonly ItemGrid playerPocketsGrid;
        private readonly ItemGrid playerStorageGrid;
        private readonly ItemGrid villagerFurnitureGrid;

        public static Item SelectedItem = Item.NO_ITEM.Clone();

        public static MainWindow Singleton() => _singleton ?? throw new Exception("MainWindow singleton not constructed!");

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            TitleBarGrid = this.FindControl<Grid>("TitleBarGrid");
            CloseGrid = this.FindControl<Grid>("CloseGrid");
            CloseButton = this.FindControl<Button>("CloseButton");
            ResizeGrid = this.FindControl<Grid>("ResizeGrid");
            ResizeButton = this.FindControl<Button>("ResizeButton");
            MinimizeGrid = this.FindControl<Grid>("MinimizeGrid");
            MinimizeButton = this.FindControl<Button>("MinimizeButton");

            SetupSide("Left", StandardCursorType.LeftSide, WindowEdge.West);
            SetupSide("Right", StandardCursorType.RightSide, WindowEdge.East);
            SetupSide("Top", StandardCursorType.TopSide, WindowEdge.North);
            SetupSide("Bottom", StandardCursorType.BottomSide, WindowEdge.South);
            SetupSide("TopLeft", StandardCursorType.TopLeftCorner, WindowEdge.NorthWest);
            SetupSide("TopRight", StandardCursorType.TopRightCorner, WindowEdge.NorthEast);
            SetupSide("BottomLeft", StandardCursorType.BottomLeftCorner, WindowEdge.SouthWest);
            SetupSide("BottomRight", StandardCursorType.BottomRightCorner, WindowEdge.SouthEast);

            TitleBarGrid.PointerPressed += (i, e) => PlatformImpl?.BeginMoveDrag(e);

            CloseGrid.PointerEnter += CloseGrid_PointerEnter;
            CloseGrid.PointerLeave += CloseGrid_PointerLeave;

            ResizeGrid.PointerEnter += ResizeGrid_PointerEnter;
            ResizeGrid.PointerLeave += ResizeGrid_PointerLeave;

            MinimizeButton.PointerLeave += MinimizeButton_PointerLeave;
            MinimizeButton.PointerEnter += MinimizeButton_PointerEnter;

            CloseButton.Click += CloseButton_Click;
            ResizeButton.Click += ResizeButton_Click;
            MinimizeButton.Click += MinimizeButton_Click;

            PlatformImpl.WindowStateChanged = WindowStateChanged;

            var openBtn = this.FindControl<Button>("OpenSaveButton");
            openBtn.Click += OpenFileButton_Click;

            this.FindControl<Button>("SaveButton").Click += SaveButton_Click;

            playerPocketsGrid = new ItemGrid(40, 10, 4, 16);
            playerStorageGrid = new ItemGrid(5000, 50, 100, 16);
            villagerFurnitureGrid = new ItemGrid(16, 8, 2, 16)
            {
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var playersGrid = this.FindControl<StackPanel>("PocketsPanel");
            playersGrid.Children.Add(playerPocketsGrid);

            this.FindControl<ScrollViewer>("StorageScroller").Content = playerStorageGrid;

            this.FindControl<StackPanel>("VillagerFurniturePanel").Children.Add(villagerFurnitureGrid);

            SetSelectedItemIndex();

            openBtn.IsVisible = true;
            this.FindControl<TabControl>("EditorTabControl").IsVisible = false;
            this.FindControl<Grid>("BottomBar").IsVisible = false;

            _singleton = this;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void SetSelectedItemIndex()
        {
            if (itemDatabase != null)
            {
                for (var i = 0; i < itemDatabase.Keys.Count; i++)
                {
                    if (itemDatabase.Keys.ElementAt(i) == SelectedItem.ItemId)
                    {
                        settingItem = true;
                        this.FindControl<ComboBox>("ItemSelectBox").SelectedIndex = i;
                        this.FindControl<NumericUpDown>("Flag0Box").Value = SelectedItem.Flags0;
                        this.FindControl<NumericUpDown>("Flag1Box").Value = SelectedItem.Flags1;
                        this.FindControl<NumericUpDown>("Flag2Box").Value = SelectedItem.Flags2;
                        this.FindControl<NumericUpDown>("Flag3Box").Value = SelectedItem.Flags3;
                        settingItem = false;
                        return;
                    }
                }
            }
            this.FindControl<ComboBox>("ItemSelectBox").SelectedIndex = -1;
        }

        public void SetItem(Item item)
        {
            if (SelectedItem != item && itemDatabase != null)
            {
                SelectedItem = item.Clone();
                SetSelectedItemIndex();
            }
        }

        private void SetupUniversalConnections()
        {
            var selectBox = this.FindControl<ComboBox>("ItemSelectBox");
            selectBox.SelectionChanged += (o, e) =>
            {
                if (!settingItem && selectBox.SelectedIndex > -1 && itemDatabase != null)
                    SelectedItem = new Item(itemDatabase.Keys.ElementAt(selectBox.SelectedIndex),
                        SelectedItem.Flags0, SelectedItem.Flags1, SelectedItem.Flags2, SelectedItem.Flags3, SelectedItem.UseCount);
            };

            this.FindControl<NumericUpDown>("Flag0Box").ValueChanged += (o, e) =>
            {
                if (!settingItem)
                    SelectedItem.Flags0 = (byte)e.NewValue;
            };
            this.FindControl<NumericUpDown>("Flag1Box").ValueChanged += (o, e) =>
            {
                if (!settingItem)
                    SelectedItem.Flags1 = (byte)e.NewValue;
            };
            this.FindControl<NumericUpDown>("Flag2Box").ValueChanged += (o, e) =>
            {
                if (!settingItem)
                    SelectedItem.Flags2 = (byte)e.NewValue;
            };
            this.FindControl<NumericUpDown>("Flag3Box").ValueChanged += (o, e) =>
            {
                if (!settingItem)
                    SelectedItem.Flags3 = (byte)e.NewValue;
            };
        }

        private void SetupPlayerTabConnections()
        {
            this.FindControl<TextBox>("PlayerNameBox").GetObservable(TextBox.TextProperty).Subscribe(text =>
            {
                if (!playerLoading && selectedPlayer != null)
                    selectedPlayer.SetName(text);
            });
            this.FindControl<NumericUpDown>("WalletBox").ValueChanged += (o, e) =>
            {
                if (!playerLoading && selectedPlayer != null)
                    selectedPlayer.Wallet.Set((uint)e.NewValue);
            };
            this.FindControl<NumericUpDown>("BankBox").ValueChanged += (o, e) =>
            {
                if (!playerLoading && selectedPlayer != null)
                    selectedPlayer.Bank.Set((uint)e.NewValue);
            };
            this.FindControl<NumericUpDown>("NookMilesBox").ValueChanged += (o, e) =>
            {
                if (!playerLoading && selectedPlayer != null)
                    selectedPlayer.NookMiles.Set((uint)e.NewValue);
            };
        }

        private void SetupVillagerTabConnections()
        {
            var villagerBox = this.FindControl<ComboBox>("VillagerBox");
            villagerBox.SelectionChanged += (o, e) => SetVillagerFromIndex(villagerBox.SelectedIndex);
            var personalityBox = this.FindControl<ComboBox>("PersonalityBox");
            personalityBox.SelectionChanged += (o, e) =>
            {
                if (selectedVillager != null)
                    selectedVillager.Personality = (byte)personalityBox.SelectedIndex;
            };
            this.FindControl<TextBox>("CatchphraseBox").GetObservable(TextBox.TextProperty).Subscribe(text =>
            {
                if (selectedVillager != null)
                    selectedVillager.Catchphrase = text;
            });
        }

        private void SetupSide(string name, StandardCursorType cursor, WindowEdge edge)
        {
            var ctl = this.FindControl<Control>(name);
            ctl.Cursor = new Cursor(cursor);
            ctl.PointerPressed += (i, e) =>
            {
                if (WindowState == WindowState.Normal)
                    PlatformImpl?.BeginResizeDrag(edge, e);
            };
        }

        private void WindowStateChanged(WindowState state)
        {
            base.HandleWindowStateChanged(state);

            if (state != WindowState.Minimized)
            {
                var img = this.FindControl<Image>("ResizeImage");
                Bitmap bitmap;
                if (WindowState == WindowState.Normal)
                {
                    bitmap = new Bitmap(AvaloniaLocator.Current.GetService<IAssetLoader>().Open(new Uri("resm:MyHorizons.Avalonia.Resources.Maximize.png")));
                }
                else
                {
                    bitmap = new Bitmap(AvaloniaLocator.Current.GetService<IAssetLoader>().Open(new Uri("resm:MyHorizons.Avalonia.Resources.Restore.png")));
                }

                img.Source?.Dispose();
                img.Source = bitmap;
            }
        }

        private void AddPlayerImages()
        {
            if (saveFile != null)
            {
                var contentHolder = this.FindControl<StackPanel>("PlayerSelectorPanel");
                foreach (var playerSave in saveFile.GetPlayerSaves())
                {
                    var player = playerSave.Player;
                    var img = new Image
                    {
                        Width = 120,
                        Height = 120,
                        Source = LoadPlayerPhoto(playerSave.Index),
                        Cursor = new Cursor(StandardCursorType.Hand)
                    };
                    var button = new Button
                    {
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Content = img
                    };
                    button.Click += (o, e) => LoadPlayer(player);
                    ToolTip.SetTip(img, playerSave.Player.GetName());
                    contentHolder.Children.Add(button);
                }
            }
        }

        private void LoadPlayer(Player player)
        {
            if (player != null && player != selectedPlayer)
            {
                playerLoading = true;
                selectedPlayer = player;
                this.FindControl<TextBox>("PlayerNameBox").Text = player.GetName();
                this.FindControl<NumericUpDown>("WalletBox").Value = player.Wallet.Decrypt();
                this.FindControl<NumericUpDown>("BankBox").Value = player.Bank.Decrypt();
                this.FindControl<NumericUpDown>("NookMilesBox").Value = player.NookMiles.Decrypt();
                playerPocketsGrid.Items = player.Pockets;
                playerStorageGrid.Items = player.Storage;
                playerLoading = false;
            }
        }

        private void LoadPatterns()
        {
            if (saveFile?.Town != null)
            {
                var panel = this.FindControl<StackPanel>("DesignsPanel");
                var editor = new PatternEditor(saveFile.Town.Patterns[0], 256, 256);
                for (var i = 0; i < 50; i++)
                {
                    var visualizer = new PatternVisualizer(saveFile.Town.Patterns[i]);
                    visualizer.PointerPressed += (o, e) => editor.SetDesign(visualizer.Design);
                    panel.Children.Add(visualizer);
                }
                this.FindControl<Grid>("DesignsContent").Children.Insert(0, editor);
            }
        }

        private void LoadVillager(Villager villager)
        {
            if (villager != null && villager != selectedVillager)
            {
                var villagerPanel = this.FindControl<StackPanel>("VillagerPanel");
                if (selectedVillager != null && villagerPanel.Children[selectedVillager.Index] is Button currentButton)
                    currentButton.Background = Brushes.Transparent;

                selectedVillager = null;
                if (villagerDatabase != null)
                {
                    var comboBox = this.FindControl<ComboBox>("VillagerBox");
                    comboBox.SelectedIndex = GetIndexFromVillagerName(villagerDatabase[villager.Species][villager.VariantIdx]);
                }
                this.FindControl<ComboBox>("PersonalityBox").SelectedIndex = villager.Personality;
                this.FindControl<TextBox>("CatchphraseBox").Text = villager.Catchphrase;
                villagerFurnitureGrid.Items = villager.Furniture;
                if (villagerPanel.Children[villager.Index] is Button btn)
                    btn.Background = Brushes.LightGray;
                selectedVillager = villager;
            }
        }

        private int GetIndexFromVillagerName(string name)
        {
            if (villagerDatabase != null)
            {
                var idx = 0;
                foreach (var species in villagerDatabase)
                {
                    foreach (var villager in species)
                    {
                        if (villager.Value == name)
                            return idx;
                        idx++;
                    }
                }
            }
            return -1;
        }

        private void SetVillagerFromIndex(int index)
        {
            if (villagerDatabase != null && selectedVillager != null && index > -1)
            {
                var count = 0;
                for (var i = 0; i < villagerDatabase.Length; i++)
                {
                    var speciesDict = villagerDatabase[i];
                    if (count + speciesDict.Count > index)
                    {
                        var species = (byte)i;
                        var variant = speciesDict.Keys.ElementAt(index - count);
                        if (selectedVillager.Species != species || selectedVillager.VariantIdx != variant)
                        {
                            selectedVillager.Species = species;
                            selectedVillager.VariantIdx = variant;

                            // Update image
                            var panel = this.FindControl<StackPanel>("VillagerPanel");
                            if (panel.Children[selectedVillager.Index] is Button btn && btn.Content is Image img)
                            {
                                img.Source?.Dispose();
                                img.Source = ImageLoadingUtil.LoadImageForVillager(selectedVillager);
                                ToolTip.SetTip(img, villagerDatabase[species][variant]);
                            }
                            return;
                        }
                    }
                    count += speciesDict.Count;
                }
            }
        }

        private void LoadVillagers()
        {
            if (saveFile != null && saveFile.Town != null)
            {
                var villagerControl = this.FindControl<StackPanel>("VillagerPanel");
                for (var i = 0; i < 10; i++)
                {
                    var villager = saveFile.Town.GetVillager(i);
                    var img = new Image
                    {
                        Width = 64,
                        Height = 64,
                        Source = ImageLoadingUtil.LoadImageForVillager(villager),
                        Cursor = new Cursor(StandardCursorType.Hand)
                    };
                    var button = new Button
                    {
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Name = $"Villager{i}",
                        Content = img
                    };
                    button.Click += (o, e) => LoadVillager(villager);
                    if (villagerDatabase != null)
                        ToolTip.SetTip(img, villagerDatabase[villager.Species][villager.VariantIdx]);
                    villagerControl.Children.Add(button);
                }
            }
        }

        private void LoadVillagerComboBoxItems()
        {
            if (villagerDatabase != null)
            {
                var comboBox = this.FindControl<ComboBox>("VillagerBox");
                var villagerList = new List<string>();
                foreach (var speciesList in villagerDatabase)
                    villagerList.AddRange(speciesList.Values);
                comboBox.Items = villagerList;
            }
            this.FindControl<ComboBox>("PersonalityBox").Items = Villager.Personalities;
        }

        private async void OpenFileButton_Click(object? o, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filters = new List<FileDialogFilter>
                    {
                        new FileDialogFilter
                        {
                            Name = "New Horizons Save File",
                            Extensions = new List<string>
                            {
                                "dat"
                            }
                        },
                        new FileDialogFilter
                        {
                            Name = "All Files",
                            Extensions = new List<string>
                            {
                                "*"
                            }
                        }
                    }
            };

            var files = await openFileDialog.ShowAsync(this);
            if (files.Length > 0)
            {
                // Determine whether they selected the header file or the main file
                var file = files[0];
                string? headerPath = null;
                string? filePath = null;
                string? directory = Path.GetDirectoryName(file);
                if (directory != null)
                {
                    if (file.EndsWith("Header.dat"))
                    {
                        headerPath = file;
                        filePath = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(file).Replace("Header", "")}.dat");
                    }
                    else
                    {
                        filePath = file;
                        headerPath = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(file)}Header.dat");
                    }
                }
                else
                {
                    return;
                }

                if (File.Exists(headerPath) && File.Exists(filePath))
                {
                    saveFile = new MainSaveFile(headerPath, filePath);
                    if (saveFile.Loaded && saveFile.Town != null)
                    {
                        villagerDatabase = VillagerDatabaseLoader.LoadVillagerDatabase((uint)saveFile.GetRevision());
                        LoadVillagerComboBoxItems();

                        if (o is Button btn)
                            btn.IsVisible = false;

                        this.FindControl<TabControl>("EditorTabControl").IsVisible = true;
                        this.FindControl<Grid>("BottomBar").IsVisible = true;
                        this.FindControl<TextBlock>("SaveInfoText").Text = $"Save File for Version {saveFile.GetRevisionString()} Loaded";
                        AddPlayerImages();
                        LoadPlayer(saveFile.GetPlayerSaves()[0].Player);
                        LoadVillagers();
                        LoadVillager(saveFile.Town.GetVillager(0));
                        LoadPatterns();

                        // Load Item List
                        itemDatabase = ItemDatabaseLoader.LoadItemDatabase((uint)saveFile.GetRevision());
                        var itemsBox = this.FindControl<ComboBox>("ItemSelectBox");
                        if (itemDatabase != null)
                            itemsBox.Items = itemDatabase.Values;

                        // Set up connections
                        SetupUniversalConnections();
                        SetupPlayerTabConnections();
                        SetupVillagerTabConnections();
                    }
                    else
                    {
                        saveFile = null;
                    }
                }
            }
        }

        private void SaveButton_Click(object? sender, RoutedEventArgs e)
        {
            if (saveFile != null)
                saveFile.Save(null);
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();

        private void ResizeButton_Click(object? sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void MinimizeButton_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void CloseGrid_PointerEnter(object? sender, PointerEventArgs e) => CloseGrid.Background = new SolidColorBrush(0xFF648589);

        private void CloseGrid_PointerLeave(object? sender, PointerEventArgs e) => CloseGrid.Background = Brushes.Transparent;

        private void ResizeGrid_PointerEnter(object? sender, PointerEventArgs e) => ResizeGrid.Background = new SolidColorBrush(0xFF648589);

        private void ResizeGrid_PointerLeave(object? sender, PointerEventArgs e) => ResizeGrid.Background = Brushes.Transparent;

        private void MinimizeButton_PointerEnter(object? sender, PointerEventArgs e) => MinimizeGrid.Background = new SolidColorBrush(0xFF648589);

        private void MinimizeButton_PointerLeave(object? sender, PointerEventArgs e) => MinimizeGrid.Background = Brushes.Transparent;

        private Bitmap? LoadPlayerPhoto(int index)
        {
            if (saveFile != null)
            {
                using var memStream = new MemoryStream(saveFile.GetPlayer(index).GetPhotoData());
                return new Bitmap(memStream);
            }
            return null;
        }
    }
}
