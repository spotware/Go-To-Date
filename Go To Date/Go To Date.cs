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

        [Parameter("Horizontal Alignment", DefaultValue = HorizontalAlignment.Left)]
        public HorizontalAlignment HorizontalAlignment { get; set; }

        [Parameter("Vertical Alignment", DefaultValue = VerticalAlignment.Top)]
        public VerticalAlignment VerticalAlignment { get; set; }

        [Parameter("Opacity", DefaultValue = 1, MinValue = 0, MaxValue = 1)]
        public double Opacity { get; set; }

        protected override void Initialize()
        {
            _chartKey = string.Format("{0}_{1}_{2}", SymbolName, TimeFrame, Chart.ChartType);

            _textBox = new TextBox
            {
                Margin = 2,
                MinWidth = 130
            };

            string timeString;

            if (_timeCache.TryGetValue(_chartKey, out timeString))
            {
                DateTime time;

                var timeStringSplit = timeString.Split('|');

                if (timeStringSplit.Length < 2 || DateTime.TryParseExact(timeStringSplit[0], _timeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out time) == false)
                {
                    _textBox.Text = _timeFormat;

                    _timeCache.TryRemove(_chartKey, out timeString);

                    return;
                }

                var utcTime = GetUtcTime(time, timeStringSplit[1]);

                if (Bars[0].OpenTime > utcTime)
                {
                    LoadMoreBars();

                    return;
                }
                else if (utcTime.HasValue)
                {
                    GoTo(utcTime.Value);
                }

                _textBox.Text = timeStringSplit[0];
            }
            else
            {
                _textBox.Text = _timeFormat;
            }

            var button = new Button
            {
                Text = "Go To",
                Margin = 2,
            };

            button.Click += Button_Click;

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment,
                VerticalAlignment = VerticalAlignment,
                Margin = 2,
                Opacity = Opacity
            };

            panel.AddChild(_textBox);
            panel.AddChild(button);

            Chart.AddControl(panel);
        }

        private void Button_Click(ButtonClickEventArgs obj)
        {
            DateTime time;

            if (DateTime.TryParseExact(_textBox.Text, _timeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out time) == false)
            {
                _textBox.Text = string.Format("Invalid date: {0}", _textBox.Text);

                return;
            }

            var timeString = string.Format("{0}|{1}", time.ToString(_timeFormat), Application.UserTimeOffset);

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
                _textBox.Text = string.Format("Data not available for: {0}", _textBox.Text);
            }
        }

        private void GoTo(DateTime utcTime)
        {
            if (utcTime > Server.TimeInUtc)
            {
                _textBox.Text = string.Format("Invalid date (Future): {0}", _textBox.Text);
            }

            Chart.ScrollXTo(utcTime);
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

        public override void Calculate(int index)
        {
        }
    }
}