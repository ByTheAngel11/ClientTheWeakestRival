using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using log4net;
using WPFTheWeakestRival.WildcardService;
using WildcardFault = WPFTheWeakestRival.WildcardService.ServiceFault;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class WildcardController
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(WildcardController));

        private readonly MatchWindowUiRefs ui;
        private readonly MatchSessionState state;

        private List<PlayerWildcardDto> myWildcards = new List<PlayerWildcardDto>();
        private PlayerWildcardDto selectedWildcard;
        private int currentWildcardIndex;

        public WildcardController(MatchWindowUiRefs ui, MatchSessionState state)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public void SelectPrev()
        {
            if (myWildcards == null || myWildcards.Count <= 1)
            {
                return;
            }

            SelectAtIndex(currentWildcardIndex - 1);
        }

        public void SelectNext()
        {
            if (myWildcards == null || myWildcards.Count <= 1)
            {
                return;
            }

            SelectAtIndex(currentWildcardIndex + 1);
        }

        public async Task LoadAsync()
        {
            if (string.IsNullOrWhiteSpace(state.Token) || state.MatchDbId <= 0)
            {
                return;
            }

            try
            {
                using (var client = new WildcardServiceClient("WSHttpBinding_IWildcardService"))
                {
                    var request = new GetPlayerWildcardsRequest
                    {
                        Token = state.Token,
                        MatchId = state.MatchDbId
                    };

                    var response = await Task.Run(() => client.GetPlayerWildcards(request));

                    IEnumerable<PlayerWildcardDto> source = response != null && response.Wildcards != null
                        ? response.Wildcards
                        : Enumerable.Empty<PlayerWildcardDto>();

                    myWildcards = source.Where(w => w != null).ToList();

                    if (myWildcards.Count == 0)
                    {
                        currentWildcardIndex = 0;
                        selectedWildcard = null;
                        UpdateUi();
                        return;
                    }

                    SelectAtIndex(myWildcards.Count - 1);
                }
            }
            catch (FaultException<WildcardFault> ex)
            {
                Logger.Warn("Fault al obtener comodines del jugador.", ex);

                MessageBox.Show(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "{0}: {1}",
                        ex.Detail != null ? ex.Detail.Code : string.Empty,
                        ex.Detail != null ? ex.Detail.Message : ex.Message),
                    MatchConstants.WILDCARDS_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                Logger.Error("Error de comunicación al obtener comodines.", ex);

                MessageBox.Show(
                    MatchConstants.DEFAULT_BANK_ERROR_MESSAGE + Environment.NewLine + ex.Message,
                    MatchConstants.WILDCARDS_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Error inesperado al obtener comodines.", ex);

                MessageBox.Show(
                    MatchConstants.DEFAULT_BANK_UNEXPECTED_ERROR_MESSAGE + Environment.NewLine + ex.Message,
                    MatchConstants.WILDCARDS_MESSAGE_TITLE,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SelectAtIndex(int index)
        {
            if (myWildcards == null || myWildcards.Count == 0)
            {
                currentWildcardIndex = 0;
                selectedWildcard = null;
                UpdateUi();
                return;
            }

            if (index < 0)
            {
                index = myWildcards.Count - 1;
            }
            else if (index >= myWildcards.Count)
            {
                index = 0;
            }

            currentWildcardIndex = index;
            selectedWildcard = myWildcards[currentWildcardIndex];

            UpdateUi();
        }

        public void InitializeEmpty()
        {
            if (ui.TxtWildcardName != null)
            {
                ui.TxtWildcardName.Text = MatchConstants.DEFAULT_NO_WILDCARD_NAME;
            }

            if (ui.TxtWildcardDescription != null)
            {
                ui.TxtWildcardDescription.Text = string.Empty;
            }

            if (ui.ImgWildcardIcon != null)
            {
                ui.ImgWildcardIcon.Visibility = Visibility.Collapsed;
                ui.ImgWildcardIcon.Source = null;
            }

            if (ui.BtnWildcardPrev != null)
            {
                ui.BtnWildcardPrev.Visibility = Visibility.Collapsed;
                ui.BtnWildcardPrev.IsEnabled = false;
            }

            if (ui.BtnWildcardNext != null)
            {
                ui.BtnWildcardNext.Visibility = Visibility.Collapsed;
                ui.BtnWildcardNext.IsEnabled = false;
            }
        }

        private void UpdateUi()
        {
            if (ui.TxtWildcardName == null || ui.TxtWildcardDescription == null)
            {
                return;
            }

            if (myWildcards == null || myWildcards.Count == 0 || selectedWildcard == null)
            {
                InitializeEmpty();
                return;
            }

            int sameCodeCount = myWildcards.Count(w => string.Equals(w.Code, selectedWildcard.Code, StringComparison.OrdinalIgnoreCase));

            string baseName = string.IsNullOrWhiteSpace(selectedWildcard.Name)
                ? selectedWildcard.Code
                : selectedWildcard.Name;

            string displayName = baseName ?? MatchConstants.DEFAULT_WILDCARD_NAME;

            if (sameCodeCount > 1)
            {
                displayName = string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} (x{1})",
                    displayName,
                    sameCodeCount);
            }

            ui.TxtWildcardName.Text = displayName;

            string baseDescription = string.IsNullOrWhiteSpace(selectedWildcard.Description)
                ? selectedWildcard.Code
                : selectedWildcard.Description;

            ui.TxtWildcardDescription.Text = sameCodeCount > 1
                ? baseDescription + Environment.NewLine + string.Format(
                    CultureInfo.CurrentCulture,
                    "Tienes {0} comodines de este tipo en esta partida.",
                    sameCodeCount)
                : baseDescription;

            bool hasMultiple = myWildcards.Count > 1;

            if (ui.BtnWildcardPrev != null)
            {
                ui.BtnWildcardPrev.Visibility = hasMultiple ? Visibility.Visible : Visibility.Collapsed;
                ui.BtnWildcardPrev.IsEnabled = hasMultiple;
            }

            if (ui.BtnWildcardNext != null)
            {
                ui.BtnWildcardNext.Visibility = hasMultiple ? Visibility.Visible : Visibility.Collapsed;
                ui.BtnWildcardNext.IsEnabled = hasMultiple;
            }

            UpdateIcon();
        }

        private void UpdateIcon()
        {
            if (ui.ImgWildcardIcon == null)
            {
                return;
            }

            if (selectedWildcard == null || string.IsNullOrWhiteSpace(selectedWildcard.Code))
            {
                ui.ImgWildcardIcon.Visibility = Visibility.Collapsed;
                ui.ImgWildcardIcon.Source = null;
                return;
            }

            try
            {
                string code = selectedWildcard.Code.Trim();
                string uriString = string.Format(
                    CultureInfo.InvariantCulture,
                    "pack://application:,,,/Assets/Wildcards/{0}.png",
                    code);

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(uriString, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                ui.ImgWildcardIcon.Source = bitmap;
                ui.ImgWildcardIcon.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Logger.WarnFormat(
                    "No se pudo cargar la imagen del comodín '{0}'.",
                    selectedWildcard != null ? selectedWildcard.Code : string.Empty);

                Logger.Warn("WildcardController.UpdateIcon", ex);

                ui.ImgWildcardIcon.Visibility = Visibility.Collapsed;
                ui.ImgWildcardIcon.Source = null;
            }
        }
    }
}
