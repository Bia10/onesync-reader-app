﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using EbookReader.DependencyService;
using EbookReader.Model.Bookshelf;
using EbookReader.Model.Messages;
using EbookReader.Page.Reader;
using EbookReader.Service;
using HtmlAgilityPack;
using Plugin.FilePicker.Abstractions;
using Xam.Plugin.WebView.Abstractions;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace EbookReader.Page {
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ReaderPage : ContentPage {

        IEpubLoader _epubLoader;
        IAssetsManager _assetsManager;
        IBookshelfService _bookshelfService;
        IMessageBus _messageBus;
        ISyncService _syncService;

        Model.EpubSpine currentChapter;

        Book _book;
        Model.Epub _epub;


        bool ResizeFirstRun = true;
        bool ResizeTimerRunning = false;
        int? ResizeTimerWidth;
        int? ResizeTimerHeight;

        public ReaderPage() {
            InitializeComponent();

            // ioc
            _epubLoader = IocManager.Container.Resolve<IEpubLoader>();
            _assetsManager = IocManager.Container.Resolve<IAssetsManager>();
            _bookshelfService = IocManager.Container.Resolve<IBookshelfService>();
            _messageBus = IocManager.Container.Resolve<IMessageBus>();
            _syncService = IocManager.Container.Resolve<ISyncService>();

            // webview events
            WebView.Messages.OnNextChapterRequest += _messages_OnNextChapterRequest;
            WebView.Messages.OnPrevChapterRequest += _messages_OnPrevChapterRequest;
            WebView.Messages.OnOpenQuickPanelRequest += _messages_OnOpenQuickPanelRequest;
            WebView.Messages.OnPageChange += Messages_OnPageChange;

            QuickPanel.PanelContent.OnChapterChange += PanelContent_OnChapterChange;

            var quickPanelPosition = new Rectangle(0, 0, 1, 0.75);

            if (Device.RuntimePlatform == Device.UWP) {
                quickPanelPosition = new Rectangle(0, 0, 0.33, 1);
            }

            _messageBus.Subscribe<ChangeMargin>((msg) => this.SetMargin(msg.Margin));
            _messageBus.Subscribe<ChangeFontSize>((msg) => this.SetFontSize(msg.FontSize));

            NavigationPage.SetHasNavigationBar(this, false);
        }

        protected override void OnDisappearing() {
            base.OnDisappearing();
            _bookshelfService.SaveBook(_book);
            _syncService.SaveProgress(_book.Id, _book.Position);
        }

        private void Messages_OnPageChange(object sender, Model.WebViewMessages.PageChange e) {
            _book.Position.SpinePosition = e.Position;
        }

        private void PanelContent_OnChapterChange(object sender, Model.Navigation.Item e) {
            var file = _epub.Files.FirstOrDefault(o => o.Href == e.Id);
            if (file != null) {
                var spine = _epub.Spines.FirstOrDefault(o => o.Idref == file.Id);
                if (spine != null) {
                    this.SendChapter(spine);
                }
            }
        }

        private void _messages_OnOpenQuickPanelRequest(object sender, Model.WebViewMessages.OpenQuickPanelRequest e) {
            QuickPanel.Show();
        }

        private async void SettingsButton_Clicked(object sender, EventArgs e) {
            await Navigation.PushAsync(App.SettingsPage());
        }

        private async void HomeButton_Clicked(object sender, EventArgs e) {
            await Navigation.PushAsync(App.HomePage());
        }

        public async void LoadBook(Book book) {
            _book = book;
            _epub = await _epubLoader.OpenEpub(book.Path);
            var position = _book.Position;

            QuickPanel.PanelContent.SetNavigation(_epub.Navigation);

            var chapter = _epub.Spines.First();
            var positionInChapter = 0;

            if (position != null && !string.IsNullOrEmpty(position.Spine)) {
                var loadedChapter = _epub.Spines.FirstOrDefault(o => o.Idref == position.Spine);
                if (loadedChapter != null) {
                    chapter = loadedChapter;
                    positionInChapter = position.SpinePosition;
                }
            }

            var syncPosition = await _syncService.LoadProgress(_book.Id);
            if (syncPosition != null && syncPosition.Position != null && syncPosition.DeviceName != UserSettings.Synchronization.DeviceName) {
                var res = await DisplayAlert("Pozice k dispozici", $"K dispozici je pozice ze zařízení {syncPosition.DeviceName}. Načíst?", "Ano", "Ne");
                if (res) {
                    var loadedChapter = _epub.Spines.FirstOrDefault(o => o.Idref == syncPosition.Position.Spine);
                    if (loadedChapter != null) {
                        chapter = loadedChapter;
                        positionInChapter = syncPosition.Position.SpinePosition;
                    }
                }
            }

            this.SendChapter(chapter, position: positionInChapter);
        }


        private void _messages_OnPrevChapterRequest(object sender, Model.WebViewMessages.PrevChapterRequest e) {
            var i = _epub.Spines.IndexOf(currentChapter);
            if (i > 0) {
                this.SendChapter(_epub.Spines[i - 1], lastPage: true);
            }
        }

        private void _messages_OnNextChapterRequest(object sender, Model.WebViewMessages.NextChapterRequest e) {
            var i = _epub.Spines.IndexOf(currentChapter);
            if (i < _epub.Spines.Count - 1) {
                this.SendChapter(_epub.Spines[i + 1]);
            }
        }

        private void WebView_OnContentLoaded(object sender, EventArgs e) {
            this.InitWebView(
                (int)WebView.Width,
                (int)WebView.Height
            );
        }

        private async void SendChapter(Model.EpubSpine chapter, int position = 0, bool lastPage = false) {
            currentChapter = chapter;
            _book.Position.Spine = chapter.Idref;

            var html = await _epubLoader.GetChapter(_epub, chapter);
            var htmlResult = await _epubLoader.PrepareHTML(html, _epub.Folder);

            Device.BeginInvokeOnMainThread(() => {
                this.SendHtml(htmlResult, position, lastPage);
            });

        }
        private void WebView_SizeChanged(object sender, EventArgs e) {

            if (ResizeFirstRun) {
                ResizeFirstRun = false;
                return;
            }

            ResizeTimerWidth = (int)WebView.Width;
            ResizeTimerHeight = (int)WebView.Height;

            if (!ResizeTimerRunning) {
                ResizeTimerRunning = true;
                Device.StartTimer(new TimeSpan(0, 0, 0, 0, 500), () => {

                    if (ResizeTimerWidth.HasValue && ResizeTimerHeight.HasValue) {
                        this.ResizeWebView(ResizeTimerWidth.Value, ResizeTimerHeight.Value);
                    }

                    ResizeTimerRunning = false;

                    return false;
                });
            }
        }

        private void GoToStartOfPageInput_TextChanged(object sender, TextChangedEventArgs e) {
            var value = e.NewTextValue;
            int page;
            if (int.TryParse(value, out page)) {
                var json = new {
                    Page = page
                };

                WebView.Messages.Send("goToStartOfPage", json);
            }
        }

        private void InitWebView(int width, int height) {
            var json = new {
                Width = width,
                Height = height,
                Margin = UserSettings.Reader.Margin,
                FontSize = UserSettings.Reader.FontSize,
                ScrollSpeed = UserSettings.Reader.ScrollSpeed,
                ClickEverywhere = UserSettings.Control.ClickEverywhere,
                DoubleSwipe = UserSettings.Control.DoubleSwipe,
            };

            WebView.Messages.Send("init", json);
        }

        private void ResizeWebView(int width, int height) {
            var json = new {
                Width = width,
                Height = height
            };

            WebView.Messages.Send("resize", json);
        }

        private void SendHtml(Model.EpubLoader.HtmlResult htmlResult, int position = 0, bool lastPage = false) {
            var json = new {
                Html = htmlResult.Html,
                Images = htmlResult.Images,
                Position = position,
                LastPage = lastPage,
            };

            WebView.Messages.Send("loadHtml", json);
        }

        private void SetFontSize(int fontSize) {
            var json = new {
                FontSize = fontSize
            };

            WebView.Messages.Send("changeFontSize", json);
        }

        private void SetMargin(int margin) {
            var json = new {
                Margin = margin
            };

            WebView.Messages.Send("changeMargin", json);
        }
    }
}