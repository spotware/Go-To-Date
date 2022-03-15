using System;
using cAlgo.API;
using System.Collections.Concurrent;
using System.Globalization;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class GoToDate : Indicator
    {
        private const string _timeFormat = "yyyy/MM/dd HH:mm:ss";

        private static ConcurrentDictionary<string, string> _timeCache = new ConcurrentDictionary<string, string>();

        private TextBox _textBox;

        private string _chartKey;

        private StackPanel _mainPanel;

        private TextBlock _errorMessageTextBlock;

        [Parameter("Horizontal Alignment", DefaultValue = HorizontalAlignment.Left, Group = "Panel")]
        public HorizontalAlignment HorizontalAlignment { get; set; }

        [Parameter("Vertical Alignment", DefaultValue = VerticalAlignment.Top, Group = "Panel")]
        public VerticalAlignment VerticalAlignment { get; set; }

        [Parameter("Opacity", DefaultValue = 1, MinValue = 0, MaxValue = 1, Group = "Panel")]
        public double Opacity { get; set; }

        [Parameter("Active", DefaultValue = true, Group = "Hotkey")]
        public bool IsHotkeyActive { get; set; }

        [Parameter("Key", DefaultValue = Key.G, Group = "Hotkey")]
        public Key Hotkey { get; set; }

        [Parameter("Modifier Key", DefaultValue = ModifierKeys.Shift, Group = "Hotkey")]
        public ModifierKeys HotkeyModifierKey { get; set; }

        protected override void Initialize()
        {
            _chartKey = string.Format("{0}_{1}_{2}", SymbolName, TimeFrame, Chart.ChartType);

            _textBox = new TextBox
            {
                Margin = 2,
                MinWidth = 130,
            };

            var button = new Button
            {
                Text = "Go To",
                Margin = 2,
            };

            button.Click += Button_Click;

            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            panel.AddChild(_textBox);
            panel.AddChild(button);

            _mainPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment,
                VerticalAlignment = VerticalAlignment,
                Margin = 2,
                Opacity = Opacity
            };

            _errorMessageTextBlock = new TextBlock { FontWeight = FontWeight.Bold };

            _mainPanel.AddChild(panel);
            _mainPanel.AddChild(_errorMessageTextBlock);

            Chart.AddControl(_mainPanel);

            string timeString;

            if (_timeCache.TryGetValue(_chartKey, out timeString))
            {
                DateTime time;

                var timeStringSplit = timeString.Split('|');

                if (timeStringSplit.Length != 2 || DateTime.TryParseExact(timeStringSplit[0], _timeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out time) == false)
                {
                    _textBox.Text = GetCurrentTime();

                    _timeCache.TryRemove(_chartKey, out timeString);
                }
                else
                {
                    var utcTime = GetUtcTime(time, timeStringSplit[1]);

                    _textBox.Text = timeStringSplit[0];

                    if (Bars[0].OpenTime > utcTime)
                    {
                        LoadMoreBars();
                    }
                    else if (utcTime.HasValue)
                    {
                        GoTo(utcTime.Value);
                    }
                }
            }
            else
            {
                _textBox.Text = GetCurrentTime();
            }

            if (IsHotkeyActive)
            {
                Chart.AddHotkey(OnHotkey, Hotkey, HotkeyModifierKey);

                _mainPanel.IsVisible = false;
            }
        }

        private void OnHotkey()
        {
            _mainPanel.IsVisible = !_mainPanel.IsVisible;
        }

        private void Button_Click(ButtonClickEventArgs obj)
        {
            DateTime time;

            if (DateTime.TryParseExact(_textBox.Text, _timeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out time) == false)
            {
                _errorMessageTextBlock.Text = string.Format("Invalid date: {0}, please use this exact format: {1}", _textBox.Text, _timeFormat);

                return;
            }

            var timeString = string.Format("{0}|{1}", time.ToString(_timeFormat, CultureInfo.InvariantCulture), Application.UserTimeOffset);

            _timeCache.AddOrUpdate(_chartKey, timeString, (key, value) => timeString);

            var utcTime = GetUtcTime(time, Application.UserTimeOffset.ToString());

            if (Bars[0].OpenTime > utcTime)
            {
                LoadMoreBars();
            }
            else if (utcTime.HasValue)
            {
                GoTo(utcTime.Value);
            }
        }

        private void LoadMoreBars()
        {
            var loadedBarsNumber = Bars.LoadMoreHistory();

            if (loadedBarsNumber == 0)
            {
                _errorMessageTextBlock.Text = string.Format("Data not available for: {0}", _textBox.Text);
            }
        }

        private void GoTo(DateTime utcTime)
        {
            if (utcTime > Server.TimeInUtc)
            {
                _errorMessageTextBlock.Text = string.Format("Invalid date (Future): {0}", _textBox.Text);
            }
            else
            {
                _timeCache.AddOrUpdate(_chartKey, _timeFormat, (key, value) => string.Format("{0}|1", value));

                Chart.ScrollXTo(utcTime);

                if (IsHotkeyActive)
                {
                    _mainPanel.IsVisible = false;
                }
            }
        }

        private DateTime? GetUtcTime(DateTime dateTime, string offsetString)
        {
            TimeSpan offset;

            if (TimeSpan.TryParse(offsetString, out offset) == false)
            {
                return null;
            }

            var dateTimeOffset = new DateTimeOffset(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, offset);

            return dateTimeOffset.UtcDateTime;
        }

        private string GetCurrentTime()
        {
            var currentUtc = new DateTimeOffset(Server.TimeInUtc.Year, Server.TimeInUtc.Month, Server.TimeInUtc.Day, Server.TimeInUtc.Hour, Server.TimeInUtc.Minute, Server.TimeInUtc.Second, TimeSpan.FromSeconds(0));

            return currentUtc.ToOffset(Application.UserTimeOffset).DateTime.ToString(_timeFormat, CultureInfo.InvariantCulture);
        }

        public override void Calculate(int index)
        {
        }
    }
}