﻿using Cryptobot.Interfaces;
using CryptoBot.Indicators;
using System;

namespace CryptoBot.Strategy.TrendReversal
{
    public class TrendReversalStrategy : IStrategy
    {
        private readonly int ZigZagCandles = 10;
        private readonly int MBFXCandles = 10;
        private readonly int MovingAveragePeriod = 15;

        private ZigZag _zigZag = new ZigZag();
        private Mbfx _mbfx = new Mbfx();
        private TrendLine _trendLine = new TrendLine();
        private Signal _signal = new Signal();

        public TrendReversalStrategy(ISymbol symbol)
        {
            Symbol = symbol;
            _signal.Symbol = symbol;
            _signal.Indicators.Add(new Indicator("ZigZag"));
            _signal.Indicators.Add(new Indicator("MBFX"));
            _signal.Indicators.Add(new Indicator("Trend"));
            _signal.Indicators.Add(new Indicator("MA15"));
        }

        public ISymbol Symbol { get; private set; }

        public Signal Process()
        {
            if (Symbol.Candles == null) return _signal;
            if (Symbol.Candles.Count == 0) return _signal;
            _zigZag.Refresh(Symbol);
            _mbfx.Refresh(Symbol);
            _trendLine.Refresh(Symbol);

            int zigZagBar = -1;
            bool zigZagBuy = false;
            bool zigZagSell = false;
            bool mbfxOk = false;
            bool trendOk = false;
            bool sma15Ok = false;

            // Rule #1: a zigzar arrow appears
            for (int bar = ZigZagCandles; bar >= 1; bar--)
            {
                var arrow = _zigZag.GetArrow(bar);
                if (arrow == ArrowType.Buy)
                {
                    zigZagBuy = true;
                    zigZagSell = false;
                    zigZagBar = bar;
                }
                if (arrow == ArrowType.Sell)
                {
                    zigZagBuy = false;
                    zigZagSell = true;
                    zigZagBar = bar;
                }
            }

            // BUY signals
            if (zigZagBuy && zigZagBar > 0)
            {
                // MBFX should be green at the moment
                // and should have been below < 30 some candles ago
                int barStart = zigZagBar;
                if (zigZagBar == 1) barStart = 2;
                for (int bar = Math.Min(barStart, MBFXCandles); bar >= 1; bar--)
                {
                    var red = _mbfx.RedValue(bar);
                    var green = _mbfx.GreenValue(bar);
                    if (red < 30M || green < 30M)
                    {
                        mbfxOk = true;
                    }
                }

                // trend line should be green at the moment
                if (_trendLine.IsGreen(1))
                {
                    trendOk = true;
                }

                // rule #4: price should be above 15 SMA
                var ma1 = MovingAverage.Get(Symbol, 1, MovingAveragePeriod, MaMethod.Sma, AppliedPrice.Close);
                if (Symbol.Candles[1].Close > ma1)
                {
                    sma15Ok = true;
                }
            }

            // SELL signals
            if (zigZagSell && zigZagBar > 0)
            {
                // MBFX should now be red
                // and should been above > 70 some candles ago
                int barStart = zigZagBar;
                if (zigZagBar == 1) barStart = 2;
                barStart = Math.Min(barStart, MBFXCandles);
                for (int bar = barStart; bar >= 1; bar--)
                {
                    var red = _mbfx.RedValue(bar);
                    var green = _mbfx.GreenValue(bar);
                    if ((red > 70M && red < 200M) || (green > 70M && green < 200M))
                    {
                        mbfxOk = true;
                    }
                }

                // trend line should now be red
                if (_trendLine.IsRed(1))
                {
                    trendOk = true;
                }

                // rule #4: and price below SMA15 on previous candle
                var ma1 = MovingAverage.Get(Symbol, 1, MovingAveragePeriod, MaMethod.Sma, AppliedPrice.Close);
                if (Symbol.Candles[1].Close < ma1)
                {
                    sma15Ok = true;
                }
            }

            // set indicators
            if (zigZagBar >= 1 && (zigZagBuy || zigZagSell))
            {
                _signal.Indicators[0].IsValid = true;
                _signal.Indicators[1].IsValid = mbfxOk;
                _signal.Indicators[2].IsValid = trendOk;
                _signal.Indicators[3].IsValid = sma15Ok;
                _signal.Type = SignalType.None;
                if (mbfxOk && trendOk && sma15Ok)
                {
                    _signal.Type = zigZagBuy ? SignalType.Buy : SignalType.Sell;
                }
            }
            return _signal;
        }
    }
}