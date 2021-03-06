﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EbookReader.Helpers;
using Newtonsoft.Json;
using EbookReader.View;
using Xamarin.Forms;

namespace EbookReader.Service {
    public class WebViewMessages {

        ReaderWebView _webView;
        bool webViewLoaded = false;
        bool webViewReaderInit = false;
        List<Model.WebViewMessages.Message> _queue;

        public event EventHandler<Model.WebViewMessages.PageChange> OnPageChange;
        public event EventHandler<Model.WebViewMessages.NextChapterRequest> OnNextChapterRequest;
        public event EventHandler<Model.WebViewMessages.PrevChapterRequest> OnPrevChapterRequest;
        public event EventHandler<Model.WebViewMessages.OpenQuickPanelRequest> OnOpenQuickPanelRequest;
        public event EventHandler<Model.WebViewMessages.ChapterRequest> OnChapterRequest;
        public event EventHandler<Model.WebViewMessages.OpenUrl> OnOpenUrl;
        public event EventHandler<Model.WebViewMessages.PanEvent> OnPanEvent;
        public event EventHandler<Model.WebViewMessages.KeyStroke> OnKeyStroke;

        public WebViewMessages(ReaderWebView webView) {
            _webView = webView;
            _queue = new List<Model.WebViewMessages.Message>();

            _webView.AddLocalCallback("csCallback", (data) => {
                this.Parse(data);
            });

            _webView.OnContentLoaded += WebView_OnContentLoaded;
        }

        public void Send(string action, object data) {

            var message = new Model.WebViewMessages.Message {
                Action = action,
                Data = data,
            };

            _queue.Add(message);
            this.ProcessQueue();
        }

        private void DoSendMessage(Model.WebViewMessages.Message message) {
            if (this.webViewLoaded && (message.Action == "init" || this.webViewReaderInit)) {
                message.IsSent = true;

                var json = JsonConvert.SerializeObject(new {
                    Action = message.Action,
                    Data = message.Data,
                });

                var toSend = Base64Helper.Encode(json);

                Device.BeginInvokeOnMainThread(async () => {
                    await _webView.InjectJavascriptAsync($"Messages.parse('{toSend}')");
                });

                if (message.Action == "init") {
                    this.webViewReaderInit = true;
                }
            }
        }

        private void Parse(string data) {
            var json = JsonConvert.DeserializeObject<Model.WebViewMessages.Message>(Base64Helper.Decode(data));

            var messageType = Type.GetType($"EbookReader.Model.WebViewMessages.{json.Action}");
            var msg = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(json.Data), messageType);

            switch (json.Action) {
                case Model.WebViewMessages.PageChange.Name:
                    this.OnPageChange?.Invoke(this, msg as Model.WebViewMessages.PageChange);
                    break;
                case Model.WebViewMessages.NextChapterRequest.Name:
                    this.OnNextChapterRequest?.Invoke(this, msg as Model.WebViewMessages.NextChapterRequest);
                    break;
                case Model.WebViewMessages.PrevChapterRequest.Name:
                    this.OnPrevChapterRequest?.Invoke(this, msg as Model.WebViewMessages.PrevChapterRequest);
                    break;
                case Model.WebViewMessages.OpenQuickPanelRequest.Name:
                    this.OnOpenQuickPanelRequest?.Invoke(this, msg as Model.WebViewMessages.OpenQuickPanelRequest);
                    break;
                case Model.WebViewMessages.ChapterRequest.Name:
                    this.OnChapterRequest?.Invoke(this, msg as Model.WebViewMessages.ChapterRequest);
                    break;
                case Model.WebViewMessages.OpenUrl.Name:
                    this.OnOpenUrl?.Invoke(this, msg as Model.WebViewMessages.OpenUrl);
                    break;
                case Model.WebViewMessages.PanEvent.Name:
                    this.OnPanEvent?.Invoke(this, msg as Model.WebViewMessages.PanEvent);
                    break;
                case Model.WebViewMessages.KeyStroke.Name:
                    this.OnKeyStroke?.Invoke(this, msg as Model.WebViewMessages.KeyStroke);
                    break;
            }

        }

        private void ProcessQueue() {

            var messages = _queue.Where(o => !o.IsSent).OrderBy(o => o.Action == "init" ? 0 : 1).ToList();

            foreach (var msg in messages) {
                this.DoSendMessage(msg);
                if (msg.IsSent) {
                    _queue.Remove(msg);
                }
            }
        }

        private void WebView_OnContentLoaded(object sender, EventArgs e) {
            this.webViewLoaded = true;
            this.ProcessQueue();
        }

    }
}