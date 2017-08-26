﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EbookReader.DependencyService;
using EbookReader.Service;
using HtmlAgilityPack;
using Plugin.FilePicker;
using Xam.Plugin.Abstractions;
using Xamarin.Forms;

namespace EbookReader {
    public class App : Application {

        FormsWebView webView;
        WebViewMessages _messages;
        Picker fontSizePicker;
        Picker marginPicker;
        Label pages;
        Picker chaptersPicker;
        int chapterPickerLastIndex = -1;

        EpubLoader epubLoader;
        Model.Epub epub;

        List<string> FontSizes {
            get {
                return new List<string> {
                    "12",
                    "14",
                    "16",
                    "18",
                    "20",
                    "22",
                    "24",
                    "26",
                    "28",
                    "30",
                    "32",
                    "34",
                    "36",
                    "38",
                    "40"
                };
            }
        }

        List<string> Margins {
            get {
                return new List<string> {
                    "15",
                    "30",
                    "45",
                };
            }
        }

        public App() {

            epubLoader = new EpubLoader();

            this.pages = new Label();

            webView = new FormsWebView() {
                ContentType = Xam.Plugin.Abstractions.Enumerations.WebViewContentType.StringData,
                VerticalOptions = LayoutOptions.FillAndExpand,
                HorizontalOptions = LayoutOptions.FillAndExpand,
            };

            _messages = new WebViewMessages(webView);
            _messages.OnPageChange += _messages_OnPageChange;
            _messages.OnNextChapterRequest += _messages_OnNextChapterRequest;
            _messages.OnPrevChapterRequest += _messages_OnPrevChapterRequest;

            var loadButton = new Button {
                Text = "Načíst knihu"
            };

            loadButton.Clicked += LoadButton_Clicked;

            var goToStartOfPageInput = new Entry();

            goToStartOfPageInput.TextChanged += GoToStartOfPageInput_TextChanged;

            fontSizePicker = new Picker {
                Title = "Velikost písma",
                ItemsSource = this.FontSizes
            };

            fontSizePicker.SelectedIndexChanged += FontSizePicker_SelectedIndexChanged;

            marginPicker = new Picker {
                Title = "Velikost odsazení",
                ItemsSource = this.Margins
            };

            marginPicker.SelectedIndexChanged += MarginPicker_SelectedIndexChanged;

            chaptersPicker = new Picker {
                Title = "Kapitola",
            };

            chaptersPicker.SelectedIndexChanged += ChaptersPicker_SelectedIndexChanged;

            this.LoadWebViewLayout();

            webView.OnContentLoaded += WebView_OnContentLoaded;
            webView.SizeChanged += WebView_SizeChanged;

            var controls = new StackLayout {
                VerticalOptions = LayoutOptions.Start,
                HorizontalOptions = LayoutOptions.FillAndExpand,
                Orientation = StackOrientation.Horizontal,
                Children = {
                    loadButton,
                    new StackLayout {
                        Children = {
                            pages,
                            goToStartOfPageInput,
                        }
                    },
                    fontSizePicker,
                    marginPicker,
                    chaptersPicker,
                }
            };

            var content = new ContentPage {
                Title = "EbookReader",
                Content = new StackLayout {
                    VerticalOptions = LayoutOptions.FillAndExpand,
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                    Children = {
                        controls,
                        webView,
                    }
                }
            };

            MainPage = new NavigationPage(content);
        }

        private void _messages_OnPrevChapterRequest(object sender, Model.WebViewMessages.PrevChapterRequest e) {
            var index = this.chaptersPicker.SelectedIndex - 1;
            if (index > 0) {
                this.chaptersPicker.SelectedIndex = index;
            }
        }

        private void _messages_OnNextChapterRequest(object sender, Model.WebViewMessages.NextChapterRequest e) {
            var index = this.chaptersPicker.SelectedIndex + 1;
            if (index < this.chaptersPicker.ItemsSource.Count) {
                this.chaptersPicker.SelectedIndex = index;
            }
        }

        private async void SendChapter(int chapter, string page = "") {
            var html = await epubLoader.GetChapter(epub, epub.Spines.Skip(chapter).First());
            var htmlResult = await epubLoader.PrepareHTML(html, epub.Folder);
            this.SendHtml(htmlResult, page);
        }

        private void ChaptersPicker_SelectedIndexChanged(object sender, EventArgs e) {
            var index = this.chaptersPicker.SelectedIndex;
            if (this.epub != null && index != -1 && index != this.chapterPickerLastIndex) {
                this.SendChapter(index, index < this.chapterPickerLastIndex ? "last" : "");
                this.chapterPickerLastIndex = index;
            }
        }

        private void _messages_OnPageChange(object sender, Model.WebViewMessages.PageChange e) {
            this.pages.Text = string.Format("Stránka {0} z {1}", e.CurrentPage, e.TotalPages);
        }

        private void MarginPicker_SelectedIndexChanged(object sender, EventArgs e) {
            if (this.marginPicker.SelectedIndex != -1) {
                var margin = int.Parse(this.Margins[this.marginPicker.SelectedIndex]);
                this.SetMargin(margin);
            }
        }

        private void WebView_SizeChanged(object sender, EventArgs e) {
            this.ResizeWebView((int)this.webView.Width, (int)this.webView.Height);
        }

        private void FontSizePicker_SelectedIndexChanged(object sender, EventArgs e) {
            if (this.fontSizePicker.SelectedIndex != -1) {
                var fontSize = int.Parse(this.FontSizes[this.fontSizePicker.SelectedIndex]);
                this.SetFontSize(fontSize);
            }
        }


        private void GoToStartOfPageInput_TextChanged(object sender, TextChangedEventArgs e) {
            var value = e.NewTextValue;
            int page;
            if (int.TryParse(value, out page)) {
                var json = new {
                    Page = page
                };

                _messages.Send("goToStartOfPage", json);
            }
        }

        private void LoadButton_Clicked(object sender, EventArgs e) {
            this.LoadBook();
        }

        public async void LoadBook() {
            var pickedFile = await CrossFilePicker.Current.PickFile();

            epub = await epubLoader.GetEpub(pickedFile.FileName, pickedFile.DataArray);

            this.chaptersPicker.ItemsSource = epub.Spines.Select(o => o.Idref).ToList();
            if (this.chaptersPicker.ItemsSource.Count > 0) {
                this.chaptersPicker.SelectedIndex = 0;
            }

            this.SendChapter(0);
        }

        private void SetFontSize(int fontSize) {
            var json = new {
                FontSize = fontSize
            };

            _messages.Send("changeFontSize", json);
        }

        private void SetMargin(int margin) {
            var json = new {
                Margin = margin
            };

            _messages.Send("changeMargin", json);
        }

        private void InitWebView(int width, int height) {
            var json = new {
                Width = width,
                Height = height
            };

            _messages.Send("init", json);
        }

        private void ResizeWebView(int width, int height) {
            var json = new {
                Width = width,
                Height = height
            };

            _messages.Send("resize", json);
        }

        private void SendHtml(Model.EpubLoader.HtmlResult htmlResult, string page) {
            var json = new {
                Html = htmlResult.Html,
                Images = htmlResult.Images,
                Page = page,
            };

            _messages.Send("loadHtml", json);
        }

        private async void LoadWebViewLayout() {
            var fileContent = Xamarin.Forms.DependencyService.Get<IAssetsManager>();

            var layout = await fileContent.GetFileContentAsync("layout.html");
            var js = await fileContent.GetFileContentAsync("reader.js");
            var css = await fileContent.GetFileContentAsync("reader.css");

            var doc = new HtmlDocument();
            doc.LoadHtml(layout);
            doc.DocumentNode.Descendants("head").First().AppendChild(HtmlNode.CreateNode(string.Format("<script>{0}</script>", js)));
            doc.DocumentNode.Descendants("head").First().AppendChild(HtmlNode.CreateNode(string.Format("<style>{0}</style>", css)));

            webView.Source = doc.DocumentNode.OuterHtml;
        }

        private void WebView_OnContentLoaded(Xam.Plugin.Abstractions.Events.Inbound.ContentLoadedDelegate eventObj) {
            this.InitWebView((int)this.webView.Width, (int)this.webView.Height);
        }

        protected override void OnStart() {
            // Handle when your app starts
        }

        protected override void OnSleep() {
            // Handle when your app sleeps
        }

        protected override void OnResume() {
            // Handle when your app resumes
        }
    }
}
