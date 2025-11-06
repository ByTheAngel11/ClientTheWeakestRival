using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPFTheWeakestRival
{
    public partial class BlackjackWindow : Window
    {
        private const int BLACKJACK_TARGET = 21;
        private const int DEALER_STAND_THRESHOLD = 17;
        private const int ACE_HIGH_VALUE = 11;
        private const int ACE_LOW_VALUE = 1;
        private const int MIN_CARD_VALUE = 2;
        private const int MAX_CARD_VALUE = 11;

        private const double CARD_WIDTH = 48.0;
        private const double CARD_HEIGHT = 68.0;
        private const double CARD_MARGIN = 4.0;
        private const double CARD_CORNER_RADIUS = 6.0;
        private const double CARD_BORDER_THICKNESS = 1.0;

        private const string CARD_SUIT_SYMBOL = "♠";

        private readonly Random random = new Random();
        private readonly List<int> playerCards = new List<int>();
        private readonly List<int> dealerCards = new List<int>();

        private static readonly Brush CardBackgroundBrush = Brushes.White;
        private static readonly Brush CardBorderBrush = Brushes.Black;
        private static readonly Brush CardTextBrush = Brushes.Black;

        private bool hasWon;

        public BlackjackWindow()
        {
            InitializeComponent();

            Loaded += BlackjackWindow_Loaded;
            Closing += BlackjackWindow_Closing;
        }

        private void BlackjackWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StartNewHand();
        }

        private void BlackjackWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!hasWon)
            {
                MessageBox.Show(
                    "Debes ganar al menos una mano de blackjack antes de salir.",
                    "Blackjack",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                e.Cancel = true;
            }
        }

        private void StartNewHand()
        {
            playerCards.Clear();
            dealerCards.Clear();

            HitCard(playerCards);
            HitCard(dealerCards);
            HitCard(playerCards);
            HitCard(dealerCards);

            UpdateUi();

            lblStatus.Text = "Tu turno: pide carta o plántate.";
            btnHit.IsEnabled = true;
            btnStand.IsEnabled = true;
            btnNewHand.IsEnabled = false;
        }

        private void HitClick(object sender, RoutedEventArgs e)
        {
            HitCard(playerCards);
            UpdateUi();

            int playerScore = CalculateHandValue(playerCards);

            if (playerScore > BLACKJACK_TARGET)
            {
                lblStatus.Text = "Te pasaste de 21. La casa gana esta mano.";
                DisablePlayerActions();
                btnNewHand.IsEnabled = true;
            }
        }

        private void StandClick(object sender, RoutedEventArgs e)
        {
            DisablePlayerActions();
            PlayDealer();
        }

        private void NewHandClick(object sender, RoutedEventArgs e)
        {
            StartNewHand();
        }

        private void PlayDealer()
        {
            int dealerScore = CalculateHandValue(dealerCards);

            while (dealerScore < DEALER_STAND_THRESHOLD)
            {
                HitCard(dealerCards);
                dealerScore = CalculateHandValue(dealerCards);
            }

            UpdateUi();

            int playerScore = CalculateHandValue(playerCards);

            if (dealerScore > BLACKJACK_TARGET || playerScore > dealerScore)
            {
                hasWon = true;

                MessageBox.Show(
                    "¡Ganaste la mano! Ahora puedes continuar.",
                    "Blackjack",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            else if (playerScore == dealerScore)
            {
                lblStatus.Text = "Empate. Nadie gana, intenta otra mano.";
                btnNewHand.IsEnabled = true;
            }
            else
            {
                lblStatus.Text = "La casa gana esta mano. Intenta de nuevo.";
                btnNewHand.IsEnabled = true;
            }
        }

        private void HitCard(ICollection<int> hand)
        {
            int value = random.Next(MIN_CARD_VALUE, MAX_CARD_VALUE + 1);
            hand.Add(value);
        }

        private static int CalculateHandValue(ICollection<int> cards)
        {
            int total = 0;
            int aceCount = 0;

            foreach (int card in cards)
            {
                if (card == ACE_HIGH_VALUE)
                {
                    total += ACE_HIGH_VALUE;
                    aceCount++;
                }
                else
                {
                    total += card;
                }
            }

            while (total > BLACKJACK_TARGET && aceCount > 0)
            {
                total -= (ACE_HIGH_VALUE - ACE_LOW_VALUE);
                aceCount--;
            }

            return total;
        }

        private void UpdateUi()
        {
            UpdateCardsPanel(playerCardsPanel, playerCards);
            UpdateCardsPanel(dealerCardsPanel, dealerCards);

            lblPlayerScore.Text = CalculateHandValue(playerCards).ToString(CultureInfo.InvariantCulture);
            lblDealerScore.Text = CalculateHandValue(dealerCards).ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateCardsPanel(StackPanel panel, IEnumerable<int> cards)
        {
            if (panel == null)
            {
                return;
            }

            panel.Children.Clear();

            foreach (int value in cards)
            {
                string label = GetCardLabel(value);

                var border = new Border
                {
                    Width = CARD_WIDTH,
                    Height = CARD_HEIGHT,
                    Margin = new Thickness(CARD_MARGIN),
                    CornerRadius = new CornerRadius(CARD_CORNER_RADIUS),
                    Background = CardBackgroundBrush,
                    BorderBrush = CardBorderBrush,
                    BorderThickness = new Thickness(CARD_BORDER_THICKNESS)
                };

                var textBlock = new TextBlock
                {
                    Text = label,
                    FontWeight = FontWeights.Bold,
                    Foreground = CardTextBrush,
                    FontSize = 18,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                border.Child = textBlock;
                panel.Children.Add(border);
            }
        }

        private static string GetCardLabel(int value)
        {
            if (value == ACE_HIGH_VALUE)
            {
                return "A" + CARD_SUIT_SYMBOL;
            }

            string numericPart = value.ToString(CultureInfo.InvariantCulture);
            return numericPart + CARD_SUIT_SYMBOL;
        }

        private void DisablePlayerActions()
        {
            btnHit.IsEnabled = false;
            btnStand.IsEnabled = false;
        }
    }
}
