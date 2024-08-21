using System;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;
using System.Linq;

namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.None)]
    public class Grid : Robot
    {
        private AverageTrueRange atr;
        private double gridSize;
        private TRM ema;

        [Parameter("ATR Period", DefaultValue = 200)]
        public int AtrPeriod { get; set; }

        [Parameter("EMA Periods", DefaultValue = 200)]
        public int EmaPeriods { get; set; }

        [Parameter("EMA Direction Multiplier", DefaultValue = 4)]
        public int EmaDirMultiplier { get; set; }

        [Parameter("Risk Percentage Per Trade", DefaultValue = 0.1)]
        public double RiskPercentagePerTrade { get; set; }

        private double currentPrice, firstUpperLevel, firstLowerLevel, secondUpperLevel, secondLowerLevel, thirdUpperLevel, thirdLowerLevel;
        private int previousTrendDirection;
        private int emaTrendDirection;
        private bool recoveryTakenInDirection = false;
        private bool recoveryTaken = false;

        protected override void OnStart()
        {
            atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Simple);
            gridSize = Math.Round(atr.Result.LastValue, Symbol.Digits);
            ema = Indicators.GetIndicator<TRM>(Bars.ClosePrices, EmaPeriods, EmaDirMultiplier);

            currentPrice = (Symbol.Bid + Symbol.Ask) / 2;
            firstUpperLevel = Math.Ceiling(currentPrice / gridSize) * gridSize;
            firstLowerLevel = Math.Floor(currentPrice / gridSize) * gridSize;
            secondUpperLevel = firstUpperLevel + gridSize;
            secondLowerLevel = firstLowerLevel - gridSize;
            thirdUpperLevel = secondUpperLevel + gridSize;
            thirdLowerLevel = secondLowerLevel - gridSize;
                
        }

        protected override void OnStop()
        {
            // Handle cBot stop here
        }

        protected override void OnBar()
        {
            
            
        }

        protected override void OnTick()
        {
            emaTrendDirection = ema.TrendDirection(Bars.ClosePrices.Count - 1);
            previousTrendDirection = ema.TrendDirection(Bars.ClosePrices.Count - 2);

            currentPrice = (Symbol.Bid + Symbol.Ask) / 2;
            double spread = Symbol.Spread;
            bool highSpread = spread > gridSize * 3;

            // calculate the upper and lower levels
            if (Symbol.Bid > secondUpperLevel || Symbol.Ask < secondLowerLevel)
            {
                firstUpperLevel = Math.Ceiling(currentPrice / gridSize) * gridSize;
                firstLowerLevel = Math.Floor(currentPrice / gridSize) * gridSize;
                secondUpperLevel = firstUpperLevel + gridSize;
                secondLowerLevel = firstLowerLevel - gridSize;
                thirdUpperLevel = secondUpperLevel + gridSize;
                thirdLowerLevel = secondLowerLevel - gridSize;
            }

            // Draw the upper and lower levels on chart
            Chart.DrawHorizontalLine("firstUpperLevel", firstUpperLevel, Color.Blue);
            Chart.DrawHorizontalLine("firstLowerLevel", firstLowerLevel, Color.Blue);
            Chart.DrawHorizontalLine("secondUpperLevel", secondUpperLevel, Color.Blue);
            Chart.DrawHorizontalLine("secondLowerLevel", secondLowerLevel, Color.Blue);
            Chart.DrawHorizontalLine("thirdUpperLevel", thirdUpperLevel, Color.Blue);
            Chart.DrawHorizontalLine("thirdLowerLevel", thirdLowerLevel, Color.Blue);

            // Calculate the volume based on the risk percentage
            double riskAmount = Account.Balance * RiskPercentagePerTrade / 100;
            double volume = riskAmount / (gridSize / Symbol.PipSize * Symbol.PipValue);
            volume = Symbol.NormalizeVolumeInUnits(volume, RoundingMode.ToNearest);

            // Check if there are already open positions or pending orders at the upper and lower levels
            bool hasFirstUpperLevelBuyOrderOrPosition = Positions.Any(position => position.Label == "Buy " + firstUpperLevel) || PendingOrders.Any(order => order.Label == "Buy " + firstUpperLevel);
            bool hasFirstUpperLevelSellOrderOrPosition = Positions.Any(position => position.Label == "Sell " + firstUpperLevel) || PendingOrders.Any(order => order.Label == "Sell " + firstUpperLevel);
            bool hasFirstLowerLevelBuyOrderOrPosition = Positions.Any(position => position.Label == "Buy " + firstLowerLevel) || PendingOrders.Any(order => order.Label == "Buy " + firstLowerLevel);
            bool hasFirstLowerLevelSellOrderOrPosition = Positions.Any(position => position.Label == "Sell " + firstLowerLevel) || PendingOrders.Any(order => order.Label == "Sell " + firstLowerLevel);
            bool hasSecondUpperLevelBuyOrderOrPosition = Positions.Any(position => position.Label == "Buy " + secondUpperLevel) || PendingOrders.Any(order => order.Label == "Buy " + secondUpperLevel);
            bool hasSecondUpperLevelSellOrderOrPosition = Positions.Any(position => position.Label == "Sell " + secondUpperLevel) || PendingOrders.Any(order => order.Label == "Sell " + secondUpperLevel);
            bool hasSecondLowerLevelBuyOrderOrPosition = Positions.Any(position => position.Label == "Buy " + secondLowerLevel) || PendingOrders.Any(order => order.Label == "Buy " + secondLowerLevel);
            bool hasSecondLowerLevelSellOrderOrPosition = Positions.Any(position => position.Label == "Sell " + secondLowerLevel) || PendingOrders.Any(order => order.Label == "Sell " + secondLowerLevel);
            bool hasThirdUpperLevelBuyOrderOrPosition = Positions.Any(position => position.Label == "Buy " + thirdUpperLevel) || PendingOrders.Any(order => order.Label == "Buy " + thirdUpperLevel);
            bool hasThirdUpperLevelSellOrderOrPosition = Positions.Any(position => position.Label == "Sell " + thirdUpperLevel) || PendingOrders.Any(order => order.Label == "Sell " + thirdUpperLevel);
            bool hasThirdLowerLevelBuyOrderOrPosition = Positions.Any(position => position.Label == "Buy " + thirdLowerLevel) || PendingOrders.Any(order => order.Label == "Buy " + thirdLowerLevel);
            bool hasThirdLowerLevelSellOrderOrPosition = Positions.Any(position => position.Label == "Sell " + thirdLowerLevel) || PendingOrders.Any(order => order.Label == "Sell " + thirdLowerLevel);

            // Display the EMA trend direction on the chart
            Chart.DrawStaticText("emaTrendDirection", "Current EMA trend direction: " + emaTrendDirection, VerticalAlignment.Top, HorizontalAlignment.Left, Color.Red);

            // Close all buy orders and stop taking buy orders if the EMA trend direction is -1
            if (emaTrendDirection == -1 || highSpread)
            {
                foreach (var order in PendingOrders.Where(o => o.TradeType == TradeType.Buy))
                {
                    CancelPendingOrder(order);
                }
            }

            // Cancel all sell orders if the EMA trend direction is 1
            if (emaTrendDirection == 1 || highSpread)
            {
                foreach (var order in PendingOrders.Where(o => o.TradeType == TradeType.Sell))
                {
                    CancelPendingOrder(order);
                }
            }

            // Place orders if there are no open positions or pending orders at the upper and lower levels
            if (!hasFirstUpperLevelBuyOrderOrPosition && emaTrendDirection != -1 && !highSpread)
            {
                PlaceStopOrder(TradeType.Buy, SymbolName, volume, firstUpperLevel, "Buy " + firstUpperLevel);
            }
            if (!hasFirstUpperLevelSellOrderOrPosition && emaTrendDirection != 1 && !highSpread)
            {
                PlaceLimitOrder(TradeType.Sell, SymbolName, volume, firstUpperLevel, "Sell " + firstUpperLevel);
            }
            if (!hasFirstLowerLevelBuyOrderOrPosition && emaTrendDirection != -1 && !highSpread)
            {
                PlaceStopOrder(TradeType.Buy, SymbolName, volume, firstLowerLevel, "Buy " + firstLowerLevel);
            }
            if (!hasFirstLowerLevelSellOrderOrPosition && emaTrendDirection != 1 && !highSpread)
            {
                PlaceLimitOrder(TradeType.Sell, SymbolName, volume, firstLowerLevel, "Sell " + firstLowerLevel);
            }
            if (!hasSecondUpperLevelBuyOrderOrPosition && emaTrendDirection != -1 && !highSpread)
            {
                PlaceStopOrder(TradeType.Buy, SymbolName, volume, secondUpperLevel, "Buy " + secondUpperLevel);
            }
            if (!hasSecondUpperLevelSellOrderOrPosition && emaTrendDirection != 1 && !highSpread)
            {
                PlaceLimitOrder(TradeType.Sell, SymbolName, volume, secondUpperLevel, "Sell " + secondUpperLevel);
            }
            if (!hasSecondLowerLevelBuyOrderOrPosition && emaTrendDirection != -1 && !highSpread)
            {
                PlaceStopOrder(TradeType.Buy, SymbolName, volume, secondLowerLevel, "Buy " + secondLowerLevel);
            }
            if (!hasSecondLowerLevelSellOrderOrPosition && emaTrendDirection != 1 && !highSpread)
            {
                PlaceLimitOrder(TradeType.Sell, SymbolName, volume, secondLowerLevel, "Sell " + secondLowerLevel);
            }
            if (!hasThirdUpperLevelBuyOrderOrPosition && emaTrendDirection != -1 && !highSpread)
            {
                PlaceStopOrder(TradeType.Buy, SymbolName, volume, thirdUpperLevel, "Buy " + thirdUpperLevel);
            }
            if (!hasThirdUpperLevelSellOrderOrPosition && emaTrendDirection != 1 && !highSpread)
            {
                PlaceLimitOrder(TradeType.Sell, SymbolName, volume, thirdUpperLevel, "Sell " + thirdUpperLevel);
            }
            if (!hasThirdLowerLevelBuyOrderOrPosition && emaTrendDirection != -1 && !highSpread)
            {
                PlaceStopOrder(TradeType.Buy, SymbolName, volume, thirdLowerLevel, "Buy " + thirdLowerLevel);
            }
            if (!hasThirdLowerLevelSellOrderOrPosition && emaTrendDirection != 1 && !highSpread)
            {
                PlaceLimitOrder(TradeType.Sell, SymbolName, volume, thirdLowerLevel, "Sell " + thirdLowerLevel);
            }


            // Update the take profit of open positions
            foreach (var position in Positions)
            {
                double takeProfitPrice = position.TradeType == TradeType.Buy ? position.EntryPrice + gridSize : position.EntryPrice - gridSize;
                if (position.TakeProfit == null) ModifyPosition(position, position.StopLoss, takeProfitPrice);
            }


            // recovery zone 
            if (emaTrendDirection == previousTrendDirection && emaTrendDirection == 0)
            {
                recoveryTakenInDirection = false;
                recoveryTaken = false;
            }

            // if the ema trend direction is the same as the previous trend direction and range, return
            if (emaTrendDirection == previousTrendDirection || emaTrendDirection == 0) return;

            // set stop loss and tp to second level, upper level tp for buy and lower level tp for sell, lower level sl for buy and upper level sl for sell. only for the positions in the opposite direction of the trend! and if the position has no sl
            foreach (var position in Positions)
            {
                if ((position.TradeType == TradeType.Buy && position.StopLoss == null && emaTrendDirection == -1) || (position.Label == "Recovery" && position.StopLoss == null))
                {
                    ModifyPosition(position, secondLowerLevel, secondUpperLevel);
                }
                else if ((position.TradeType == TradeType.Sell && position.StopLoss == null && emaTrendDirection == 1) || (position.Label == "Recovery" && position.StopLoss == null))
                {
                    ModifyPosition(position, secondUpperLevel, secondLowerLevel);
                }
            }

            // make recoveryTaken not true when price reverses to the first most far away level
            if (recoveryTaken)
            {
                if ((recoveryTakenInDirection && emaTrendDirection == 1 && Symbol.Ask < firstLowerLevel) || (!recoveryTakenInDirection && emaTrendDirection == 1 && Symbol.Bid > firstUpperLevel) || (recoveryTakenInDirection && emaTrendDirection == -1 && Symbol.Bid > firstUpperLevel) || (!recoveryTakenInDirection && emaTrendDirection == -1 && Symbol.Ask < firstLowerLevel))
                {
                    recoveryTaken = false;
                }
            }

            // if !recoveryTaken, place a order with label recovery and tp and sl at the first level, upper level tp for buy and lower level tp for sell, lower level sl for buy and upper level sl for sell. if recoveryTakenInDirection then place a trade in opposite direction of the trend. make recoveryTaken true
            if (!recoveryTaken)
            {
                if (emaTrendDirection == 1)
                {
                    if (!recoveryTakenInDirection)
                    {
                        PlaceStopOrder(TradeType.Sell, SymbolName, volume, firstUpperLevel, "Recovery");
                        recoveryTakenInDirection = true;
                    }
                    else
                    {
                        PlaceStopOrder(TradeType.Buy, SymbolName, volume, firstLowerLevel, "Recovery");
                        recoveryTakenInDirection = false;
                    }
                }
                else if (emaTrendDirection == -1)
                {
                    if (!recoveryTakenInDirection)
                    {
                        PlaceStopOrder(TradeType.Buy, SymbolName, volume, firstLowerLevel, "Recovery");
                        recoveryTakenInDirection = true;
                    }
                    else
                    {
                        PlaceStopOrder(TradeType.Sell, SymbolName, volume, firstUpperLevel, "Recovery");
                        recoveryTakenInDirection = false;
                    }
                }
                recoveryTaken = true;
            }


        }
    }
}
