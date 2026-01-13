using log4net;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WPFTheWeakestRival.Infrastructure;
using WPFTheWeakestRival.LobbyService;
using WPFTheWeakestRival.Properties.Langs;

namespace WPFTheWeakestRival.Infraestructure.Lobby
{
    internal sealed class LobbyChatController : IDisposable
    {
        private const int RecentEchoWindowSeconds = 2;
        private const string UnknownAuthorDisplayName = "?";
        private const string ChatTimeFormat = "HH:mm";
        private const int PendingRetryIntervalSeconds = 5;

        private readonly LobbyUiDispatcher ui;
        private readonly LobbyRuntimeState state;
        private readonly ILog logger;

        private readonly ObservableCollection<ChatLine> chatLines;
        private readonly ObservableCollection<PendingMessage> pendingMessages;

        private readonly ListBox chatList;
        private readonly TextBox chatInput;

        private readonly DispatcherTimer pendingRetryTimer;

        private bool isRetryingPending;

        private string lastSentText = string.Empty;
        private DateTime lastSentUtc = DateTime.MinValue;

        internal LobbyChatController(
            LobbyUiDispatcher ui,
            LobbyRuntimeState state,
            ListBox chatList,
            TextBox chatInput,
            ILog logger = null)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.chatList = chatList;
            this.chatInput = chatInput;
            this.logger = logger ?? LogManager.GetLogger(typeof(LobbyChatController));

            chatLines = new ObservableCollection<ChatLine>();
            pendingMessages = new ObservableCollection<PendingMessage>();

            if (this.chatList != null)
            {
                this.chatList.ItemsSource = chatLines;
            }

            pendingRetryTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(PendingRetryIntervalSeconds)
            };
            pendingRetryTimer.Tick += PendingRetryTimerTick;

            AppServices.Lobby.ChatMessageReceived += OnChatMessageReceivedFromHub;
            AppServices.Lobby.ChatSendFailed += OnChatSendFailed;
        }

        internal void HandleChatInputKeyDown(KeyEventArgs e)
        {
            if (e == null || e.Key != Key.Enter)
            {
                return;
            }

            e.Handled = true;

            var messageText = (chatInput?.Text ?? string.Empty).Trim();
            if (messageText.Length == 0)
            {
                return;
            }

            if (!state.CurrentLobbyId.HasValue)
            {
                MessageBox.Show(Lang.chatCreateOrJoinFirst);
                return;
            }

            string token = SessionTokenProvider.GetTokenOrShowMessage();
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            _ = SendMessageAsync(token, state.CurrentLobbyId.Value, messageText);
        }

        internal void AppendSystemLine(string text)
        {
            AppendLine(Lang.system, text);
        }

        private async Task SendMessageAsync(string token, Guid lobbyId, string messageText)
        {
            try
            {
                AppendLine(state.MyDisplayName, messageText);

                lastSentText = messageText;
                lastSentUtc = DateTime.UtcNow;

                await AppServices.Lobby.SendMessageAsync(token, lobbyId, messageText);

                if (chatInput != null)
                {
                    chatInput.Text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                logger.Error("LobbyChatController.SendMessageAsync error.", ex);

                EnqueuePendingMessage(token, lobbyId, messageText);

                MessageBox.Show(
                    Lang.noConnection,
                    Lang.chatTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                if (chatInput != null)
                {
                    chatInput.Text = string.Empty;
                }
            }
        }

        private void AppendLine(string author, string text)
        {
            chatLines.Add(
                new ChatLine
                {
                    Author = string.IsNullOrWhiteSpace(author) ? UnknownAuthorDisplayName : author,
                    Text = text ?? string.Empty,
                    Time = DateTime.Now.ToString(ChatTimeFormat, CultureInfo.InvariantCulture)
                });

            if (chatList != null && chatList.Items.Count > 0)
            {
                chatList.ScrollIntoView(chatList.Items[chatList.Items.Count - 1]);
            }
        }

        private void EnqueuePendingMessage(string token, Guid lobbyId, string text)
        {
            pendingMessages.Add(
                new PendingMessage
                {
                    Token = token ?? string.Empty,
                    LobbyId = lobbyId,
                    Text = text ?? string.Empty,
                    QueuedUtc = DateTime.UtcNow
                });

            AppendSystemLine(Lang.noConnection);

            if (!pendingRetryTimer.IsEnabled)
            {
                pendingRetryTimer.Start();
            }
        }

        private void PendingRetryTimerTick(object sender, EventArgs e)
        {
            _ = RetryPendingMessagesAsync();
        }

        private async Task RetryPendingMessagesAsync()
        {
            if (isRetryingPending)
            {
                return;
            }

            if (pendingMessages.Count == 0)
            {
                if (pendingRetryTimer.IsEnabled)
                {
                    pendingRetryTimer.Stop();
                }

                return;
            }

            isRetryingPending = true;

            try
            {
                var copy = new PendingMessage[pendingMessages.Count];
                pendingMessages.CopyTo(copy, 0);

                foreach (var pm in copy)
                {
                    try
                    {
                        if (!state.CurrentLobbyId.HasValue || state.CurrentLobbyId.Value != pm.LobbyId)
                        {
                            pendingMessages.Remove(pm);
                            continue;
                        }

                        await AppServices.Lobby.SendMessageAsync(pm.Token, pm.LobbyId, pm.Text);

                        pendingMessages.Remove(pm);
                    }
                    catch (Exception ex)
                    {
                        logger.Warn("Retry pending message failed.", ex);
                    }
                }
            }
            finally
            {
                isRetryingPending = false;
            }
        }

        private void OnChatSendFailed(Exception ex)
        {
            if (ex == null)
            {
                return;
            }

            ui.Ui(() => AppendSystemLine(Lang.noConnection));
        }

        private void OnChatMessageReceivedFromHub(ChatMessage chat)
        {
            try
            {
                if (chat == null)
                {
                    return;
                }

                ui.Ui(() =>
                {
                    var author = string.IsNullOrWhiteSpace(chat.FromPlayerName)
                        ? Lang.player
                        : chat.FromPlayerName;

                    var isMyRecentEcho =
                        string.Equals(author, state.MyDisplayName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(chat.Message ?? string.Empty, lastSentText, StringComparison.Ordinal) &&
                        (DateTime.UtcNow - lastSentUtc) < TimeSpan.FromSeconds(RecentEchoWindowSeconds);

                    if (!isMyRecentEcho)
                    {
                        AppendLine(author, chat.Message ?? string.Empty);
                    }
                });
            }
            catch (Exception ex)
            {
                logger.Error("OnChatMessageReceivedFromHub error.", ex);
            }
        }

        public void Dispose()
        {
            AppServices.Lobby.ChatMessageReceived -= OnChatMessageReceivedFromHub;
            AppServices.Lobby.ChatSendFailed -= OnChatSendFailed;

            if (pendingRetryTimer.IsEnabled)
            {
                pendingRetryTimer.Stop();
            }

            pendingRetryTimer.Tick -= PendingRetryTimerTick;
        }

        private sealed class ChatLine
        {
            public string Author { get; set; } = string.Empty;

            public string Text { get; set; } = string.Empty;

            public string Time { get; set; } = string.Empty;
        }

        private sealed class PendingMessage
        {
            public string Token { get; set; } = string.Empty;

            public Guid LobbyId { get; set; }

            public string Text { get; set; } = string.Empty;

            public DateTime QueuedUtc { get; set; }
        }
    }
}
