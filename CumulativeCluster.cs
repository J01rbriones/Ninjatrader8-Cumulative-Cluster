using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
using System.ComponentModel.DataAnnotations;

namespace NinjaTrader.NinjaScript.Indicators.PrecisionTrade
{
    public class CumulativeCluster : Indicator
    {
        private double buys;
        private double sells;
        private double clusterThreshold = 1;
        private double clusterSize = 1;
        private static double globalTotalBuys;
        private static double globalTotalSells;
        private static readonly object globalDataLock = new object();
        private List<double> lastBuyClusters = new List<double>();
        private List<double> lastSellClusters = new List<double>();
        private double buyClustersTotal = 0;
        private double sellClustersTotal = 0;
        private int lastBarMarketData = -1;
        private const int DefaultClusterSeparationTicks = 1;
        private const int MaxClusterSeparationTicks = 3;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Order Flow Cumulative Clusters";
                Name = "Cumulative Cluster";
                Calculate = Calculate.OnEachTick;
                DrawOnPricePanel = true;
                IsOverlay = false;
                DisplayInDataBox = false;
                PaintPriceMarkers = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
            }
            else if (State == State.Configure)
            {
                AddPlot(Brushes.Gray, "Delta");
                Plots[0].PlotStyle = PlotStyle.Bar;
                Plots[0].AutoWidth = true;
                AddDataSeries(BarsPeriodType.Tick, 1);
            }
        }

        protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        {
            if (marketDataUpdate.MarketDataType == MarketDataType.Last)
            {
                int currentBarForMarketData = CurrentBar;
                if (currentBarForMarketData != lastBarMarketData)
                {
                    buys = 0;
                    sells = 0;
                    lastBarMarketData = currentBarForMarketData;
                }

                double volume = marketDataUpdate.Volume;
                double price = marketDataUpdate.Price;
                double bid = marketDataUpdate.Bid;
                double ask = marketDataUpdate.Ask;
                double contractMultiplier = GetContractValueMultiplier(Instrument.FullName);

                if (price >= ask)
                {
                    buys += volume;
                    if (volume * contractMultiplier >= OutputThreshold)
                        PrintToOutputWindow($"Buy Order (Ask), Value: {FormatValue(volume * contractMultiplier)}, Price: {price:N2}, Instrument: {Instrument.FullName}");
                    lock (globalDataLock) globalTotalBuys += (volume * contractMultiplier);
                }
                else if (price <= bid)
                {
                    sells += volume;
                    if (volume * contractMultiplier >= OutputThreshold)
                        PrintToOutputWindow($"Sell Order (Bid), Value: {FormatValue(volume * contractMultiplier)}, Price: {price:N2}, Instrument: {Instrument.FullName}");
                    lock (globalDataLock) globalTotalSells += (volume * contractMultiplier);
                }
            }
        }

        protected override void OnBarUpdate()
        {
            if (BarsInProgress != 0) return;
            Values[0][0] = buys - sells;
            PlotBrushes[0][0] = (Values[0][0] > 0) ? PositiveBrush : NegativeBrush;
            double contractMultiplier = GetContractValueMultiplier(Instrument.FullName);
            bool buyCandidate = buys > clusterThreshold;
            bool sellCandidate = sells > clusterThreshold;
            double buyValue = buys * contractMultiplier;
            double sellValue = sells * contractMultiplier;
            string tag = "ClusterPoint" + CurrentBar;
            if (buyCandidate || sellCandidate)
            {
                bool drawBuy = false;
                bool drawSell = false;
                if (buyCandidate && sellCandidate)
                {
                    if (Math.Abs(buyValue - sellValue) < 1e-9) { }
                    else if (buyValue > sellValue) drawBuy = true;
                    else drawSell = true;
                }
                else if (buyCandidate) drawBuy = true;
                else drawSell = true;
                if (drawBuy)
                {
                    RenderBuyClusterPointDynamic(tag, buyValue);
                    AddToClusterList(lastBuyClusters, buyValue);
                    if (buyValue >= OutputThreshold) PrintToOutputWindow2($"Buy Cluster (Ask), Value: {FormatValue(buyValue)}, Price: {Close[0]}, Instrument: {Instrument.FullName}");
                }
                else if (drawSell)
                {
                    RenderSellClusterPointDynamic(tag, sellValue);
                    AddToClusterList(lastSellClusters, sellValue);
                    if (sellValue >= OutputThreshold) PrintToOutputWindow2($"Sell Cluster (Bid), Value: {FormatValue(sellValue)}, Price: {Close[0]}, Instrument: {Instrument.FullName}");
                }
            }
            double currentGlobalTotalBuysVal;
            double currentGlobalTotalSellsVal;
            lock (globalDataLock) { currentGlobalTotalBuysVal = globalTotalBuys; currentGlobalTotalSellsVal = globalTotalSells; }
            string sellAggressiveText = "Sell Aggressive (Bid):\n" + string.Join("\n", lastSellClusters.Select(v => FormatValue(v)).Reverse()) + $"\nTotal Local: {FormatValue(sellClustersTotal)}" + $"\nGlobal Orders Total: {FormatValue(currentGlobalTotalSellsVal)}";
            string buyAggressiveText = "Buy Aggressive (Ask):\n" + string.Join("\n", lastBuyClusters.Select(v => FormatValue(v)).Reverse()) + $"\nTotal Local: {FormatValue(buyClustersTotal)}" + $"\nGlobal Orders Total: {FormatValue(currentGlobalTotalBuysVal)}";
            Draw.TextFixed(this, "SellAggressiveVolumes", sellAggressiveText, TextPosition.TopLeft, Brushes.White, new SimpleFont("Arial", 10), Brushes.Transparent, Brushes.Black, 80);
            Draw.TextFixed(this, "BuyAggressiveVolumes", buyAggressiveText, TextPosition.TopRight, Brushes.White, new SimpleFont("Arial", 10), Brushes.Transparent, Brushes.Black, 80);
        }

        private void RenderBuyClusterPointDynamic(string tag, double buyValue)
        {
            double baseRadiusPrice = ClusterCircleRadiusTicks * TickSize;
            int separationTicks = Math.Min(DefaultClusterSeparationTicks, MaxClusterSeparationTicks);
            double offset = separationTicks * TickSize;
            double radiusScale = ComputeIntensityAndScale(buyValue, out double intensity);
            double radiusPrice = baseRadiusPrice * radiusScale;
            double maxAllowedRadius = Math.Max(TickSize, (High[0] - Low[0]) * 0.5);
            radiusPrice = Math.Min(radiusPrice, maxAllowedRadius);
            double centerPrice = (High[0] + Low[0]) / 2 - offset - radiusPrice;
            Brush fill = CreateSemiTransparentBrush(BuyClusterColor, ClusterCircleOpacity * intensity);
            Brush stroke = CreateSemiTransparentBrush(BuyClusterColor, Math.Min(1.0, ClusterCircleOpacity * intensity * 1.6));
            double top = centerPrice + radiusPrice;
            double bottom = centerPrice - radiusPrice;
            Draw.Ellipse(this, tag, true, 0, top, 0, bottom, stroke, fill, ClusterCircleStrokeWidth);
        }

        private void RenderSellClusterPointDynamic(string tag, double sellValue)
        {
            double baseRadiusPrice = ClusterCircleRadiusTicks * TickSize;
            int separationTicks = Math.Min(DefaultClusterSeparationTicks, MaxClusterSeparationTicks);
            double offset = separationTicks * TickSize;
            double radiusScale = ComputeIntensityAndScale(sellValue, out double intensity);
            double radiusPrice = baseRadiusPrice * radiusScale;
            double maxAllowedRadius = Math.Max(TickSize, (High[0] - Low[0]) * 0.5);
            radiusPrice = Math.Min(radiusPrice, maxAllowedRadius);
            double centerPrice = (High[0] + Low[0]) / 2 + offset + radiusPrice;
            Brush fill = CreateSemiTransparentBrush(SellClusterColor, ClusterCircleOpacity * intensity);
            Brush stroke = CreateSemiTransparentBrush(SellClusterColor, Math.Min(1.0, ClusterCircleOpacity * intensity * 1.6));
            double top = centerPrice + radiusPrice;
            double bottom = centerPrice - radiusPrice;
            Draw.Ellipse(this, tag, true, 0, top, 0, bottom, stroke, fill, ClusterCircleStrokeWidth);
        }

        private double ComputeIntensityAndScale(double valueInMoney, out double intensity)
        {
            double denom = Math.Max(1.0, OutputThreshold * ClusterNormalizationMultiplier);
            intensity = Math.Min(1.0, valueInMoney / denom);
            return 1.0 + (ClusterCircleMaxScale - 1.0) * intensity;
        }

        private Brush CreateSemiTransparentBrush(Brush baseBrush, double opacity)
        {
            double clamped = Math.Max(0.0, Math.Min(1.0, opacity));
            if (baseBrush is SolidColorBrush sb) { Color c = sb.Color; return new SolidColorBrush(Color.FromArgb((byte)(clamped * 255), c.R, c.G, c.B)); }
            return new SolidColorBrush(Color.FromArgb((byte)(clamped * 255), 0, 0, 0));
        }

        private void PrintToOutputWindow2(string message) { NinjaTrader.Code.Output.Process(message, PrintTo.OutputTab2); }
        private void PrintToOutputWindow(string message) { NinjaTrader.Code.Output.Process(message, PrintTo.OutputTab1); }

        private double GetContractValueMultiplier(string instrumentName)
        {
            if (string.IsNullOrEmpty(instrumentName)) return 500;
            string upperInstrumentName = instrumentName.ToUpperInvariant();
            if (upperInstrumentName.StartsWith("ES")) return 500;
            if (upperInstrumentName.StartsWith("MES")) return 50;
            if (upperInstrumentName.StartsWith("NQ")) return 1000;
            if (upperInstrumentName.StartsWith("MNQ")) return 100;
            if (upperInstrumentName.StartsWith("YM")) return 500;
            if (upperInstrumentName.StartsWith("MYM")) return 50;
            if (upperInstrumentName.StartsWith("RTY")) return 500;
            if (upperInstrumentName.StartsWith("M2K")) return 500;
            if (Instrument != null && instrumentName == Instrument.FullName) return Instrument.MasterInstrument.PointValue;
            return 500;
        }

        private string FormatValue(double value)
        {
            if (Math.Abs(value) >= 1_000_000_000_000) return string.Format("${0:0.#}T", value / 1_000_000_000_000);
            if (Math.Abs(value) >= 1_000_000_000) return string.Format("${0:0.#}B", value / 1_000_000_000);
            if (Math.Abs(value) >= 1_000_000) return string.Format("${0:0.#}M", value / 1_000_000);
            if (Math.Abs(value) >= 1_000) return string.Format("${0:0.#}K", value / 1_000);
            return string.Format("${0:N0}", value);
        }

        private void AddToClusterList(List<double> clusterList, double valueInMoney)
        {
            if (clusterList.Count >= 35) clusterList.RemoveAt(0);
            clusterList.Add(valueInMoney);
            if (clusterList == lastBuyClusters) buyClustersTotal += valueInMoney;
            else if (clusterList == lastSellClusters) sellClustersTotal += valueInMoney;
        }

        [XmlIgnore][Display(Name = "Color de barra positiva", GroupName = "Visual", Order = 1)] public Brush PositiveBrush { get; set; } = Brushes.Lime;
        [Browsable(false)] public string PositiveBrushSerializable { get { return Serialize.BrushToString(PositiveBrush); } set { PositiveBrush = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name = "Color de barra negativa", GroupName = "Visual", Order = 2)] public Brush NegativeBrush { get; set; } = Brushes.DarkRed;
        [Browsable(false)] public string NegativeBrushSerializable { get { return Serialize.BrushToString(NegativeBrush); } set { NegativeBrush = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name = "Color clúster compra", GroupName = "Visual", Order = 3)] public Brush BuyClusterColor { get; set; } = Brushes.Lime;
        [Browsable(false)] public string BuyClusterColorSerializable { get { return Serialize.BrushToString(BuyClusterColor); } set { BuyClusterColor = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name = "Color clúster venta", GroupName = "Visual", Order = 4)] public Brush SellClusterColor { get; set; } = Brushes.DarkRed;
        [Browsable(false)] public string SellClusterColorSerializable { get { return Serialize.BrushToString(SellClusterColor); } set { SellClusterColor = Serialize.StringToBrush(value); } }
        [Range(1, double.MaxValue), NinjaScriptProperty][Display(Name = "Umbral clúster (volumen)", Description = "Volumen mínimo para considerar clúster", Order = 1, GroupName = "Parámetros")] public double ClusterThreshold { get { return clusterThreshold; } set { clusterThreshold = value; } }
        [Range(1, double.MaxValue), NinjaScriptProperty][Display(Name = "Tamaño de clúster (ticks)", Description = "Tamaño del clúster en ticks", Order = 2, GroupName = "Parámetros")] public double ClusterSize { get { return clusterSize; } set { clusterSize = value; } }
        [Range(0, double.MaxValue), NinjaScriptProperty][Display(Name = "Umbral de salida ($)", Description = "Valor mínimo en $ para mensajes en la ventana de salida", Order = 3, GroupName = "Parámetros")] public double OutputThreshold { get; set; } = 10;
        [Range(1, 50), NinjaScriptProperty][Display(Name = "Radio del punto del clúster (ticks)", Description = "Radio base del punto en ticks", Order = 20, GroupName = "Visual")] public int ClusterCircleRadiusTicks { get; set; } = 2;
        [Range(0.01, 1.0), NinjaScriptProperty][Display(Name = "Opacidad del punto del clúster", Description = "Opacidad del relleno del punto", Order = 21, GroupName = "Visual")] public double ClusterCircleOpacity { get; set; } = 0.7;
        [Range(0.1, 100.0), NinjaScriptProperty][Display(Name = "Multiplicador de normalización", Description = "Normalización para mapear valor $ a intensidad", Order = 22, GroupName = "Parámetros")] public double ClusterNormalizationMultiplier { get; set; } = 10.0;
        [Range(0.5, 4.0), NinjaScriptProperty][Display(Name = "Escala máxima del punto", Description = "Multiplicador máximo que escala el radio según intensidad", Order = 23, GroupName = "Visual")] public double ClusterCircleMaxScale { get; set; } = 1.6;
        [Range(0, 5), NinjaScriptProperty][Display(Name = "Ancho del borde del punto", Description = "Ancho del borde del punto", Order = 24, GroupName = "Visual")] public int ClusterCircleStrokeWidth { get; set; } = 1;
    }
}
