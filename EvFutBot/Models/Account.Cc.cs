﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EvFutBot.Utilities;
using MySql.Data.MySqlClient;
using Renci.SshNet.Common;
using UltimateTeam.Toolkit.Exceptions;
using UltimateTeam.Toolkit.Models;
using UltimateTeam.Toolkit.Parameters;

namespace EvFutBot.Models
{
    public partial class Account
    {
        public async Task BuyCustomerCards(Settings settings, DateTime startedAt)
        {
            var tosell = Credits - SmallAccount;
            if (Credits <= SmallAccount || _cardsPerHour >= MaxCardsPerHour || tosell < 10000) return;
            var cards = GetCustomerCards(tosell);
            if (cards.Count == 0) return;

            foreach (var card in cards)
            {
                await SearchAndBuyCc(card, settings, startedAt);
            }
        }

        public async Task<bool> SearchAndBuyCc(OrderCard card, Settings settings, DateTime startedAt)
        {
            if (card.Bin >= Credits)
            {
                MarkResetCard(card.TradeId);
                return false;
            }

            AuctionResponse searchResponse;
            var searchParameters = new PlayerSearchParameters
            {
                Page = 1,
                ResourceId = card.BaseId,
                MinBid = card.StartPrice < 150 ? 150 : card.StartPrice,
                MaxBuy = card.Bin,
                MinBuy = AuctionInfo.CalculatePreviousBid(card.Bin)
            };

            try
            {
                await Task.Delay(settings.RmpDelay);
                searchResponse = await _utClient.SearchAsync(searchParameters);

                while (searchResponse.AuctionInfo.Any(auction => auction.TradeId == card.TradeId) == false &&
                       searchResponse.AuctionInfo.Count == 13)
                {
                    searchParameters.Page++;
                    await Task.Delay(settings.RmpDelay);
                    searchResponse = await _utClient.SearchAsync(searchParameters);
                }

                // if we can't find the card we try again
                if (searchResponse.AuctionInfo.Any(auction => auction.TradeId == card.TradeId) == false)
                {
                    searchParameters.Page = 1;
                    await Task.Delay(settings.RmpDelay);
                    searchResponse = await _utClient.SearchAsync(searchParameters);

                    while (searchResponse.AuctionInfo.Any(auction => auction.TradeId == card.TradeId) == false &&
                           searchResponse.AuctionInfo.Count == 13)
                    {
                        searchParameters.Page++;
                        await Task.Delay(settings.RmpDelay);
                        searchResponse = await _utClient.SearchAsync(searchParameters);
                    }
                }

                // if we still can't find the card
                if (searchResponse.AuctionInfo.Any(auction => auction.TradeId == card.TradeId) == false)
                {
                    MarkErrorCard("Card Not Found!");
                    return false;
                }
            }
            catch (ExpiredSessionException ex)
            {
                MarkResetCard(card.TradeId);
                await HandleException(ex, settings.SecurityDelay, Email);
                return false;
            }
            catch (ArgumentException ex)
            {
                MarkResetCard(card.TradeId);
                await HandleException(ex, settings.SecurityDelay, Email);
                return false;
            }
            catch (CaptchaTriggeredException ex)
            {
                MarkResetCard(card.TradeId);
                await HandleException(ex, Email);
                return false;
            }
            catch (HttpRequestException ex)
            {
                MarkResetCard(card.TradeId);
                await HandleException(ex, settings.SecurityDelay, Email);
                return false;
            }
            catch (Exception ex)
            {
                MarkResetCard(card.TradeId);
                await HandleException(ex, settings.SecurityDelay, Email);
                return false;
            }

            foreach (var auction in searchResponse.AuctionInfo.Where(
                auction => auction.TradeId == card.TradeId))
            {
                if (card.Bin >= Credits)
                {
                    MarkResetCard(card.TradeId);
                    continue;
                }
                try
                {
                    AuctionResponse placeBid;
                    try
                    {
                        if (auction.TradeId != card.TradeId) continue;
                        await Task.Delay(settings.RmpDelay);
                        placeBid = await _utClient.PlaceBidAsync(auction, card.Bin);
                        if (placeBid.AuctionInfo == null)
                        {
                            MarkErrorCard("Buy Error!");
                            continue;
                        }
                    }
                    catch (PermissionDeniedException)
                    {
                        MarkResetCard(card.TradeId);
                        return false;
                    }
                    catch (NoSuchTradeExistsException)
                    {
                        MarkResetCard(card.TradeId);
                        return false;
                    }
                    catch (ExpiredSessionException ex)
                    {
                        MarkResetCard(card.TradeId);
                        await HandleException(ex, settings.SecurityDelay, Email);
                        return false;
                    }
                    catch (ArgumentException ex)
                    {
                        MarkResetCard(card.TradeId);
                        await HandleException(ex, settings.SecurityDelay, Email);
                        return false;
                    }
                    catch (CaptchaTriggeredException ex)
                    {
                        MarkResetCard(card.TradeId);
                        await HandleException(ex, Email);
                        return false;
                    }
                    catch (HttpRequestException ex)
                    {
                        MarkResetCard(card.TradeId);
                        await HandleException(ex, settings.SecurityDelay, Email);
                        return false;
                    }
                    catch (Exception ex)
                    {
                        MarkResetCard(card.TradeId);
                        await HandleException(ex, settings.SecurityDelay, Email);
                        return false;
                    }

                    var boughtAction = placeBid.AuctionInfo.FirstOrDefault();
                    if (boughtAction != null && boughtAction.TradeState == "closed")
                    {
                        MarkBoughtCard(card.TradeId); // deal one.. now relisting

                        await Task.Delay(settings.RmpDelayLow);
                        var tradePileResponse = await _utClient.SendItemToTradePileAsync(boughtAction.ItemData);
                        var tradeItem = tradePileResponse.ItemData.FirstOrDefault();

                        if (tradeItem != null)
                        {
                            _cardsPerHour++;
                            AuctionDetails auctionDetails = null;
                            if (card.Value <= QuickSellLimit)
                            {
                                try
                                {
                                    await Task.Delay(settings.RmpDelay*4);
                                    await _utClient.QuickSellItemAsync(boughtAction.ItemData.Id);
                                }
                                catch (Exception ex)
                                {
                                    try
                                    {
                                        await Task.Delay(settings.RmpDelay*4);
                                        await _utClient.ListAuctionAsync(new AuctionDetails(boughtAction.ItemData.Id,
                                            GetAuctionDuration(startedAt, settings.RunforHours, Login),
                                            boughtAction.ItemData.MarketDataMinPrice,
                                            boughtAction.ItemData.MarketDataMaxPrice));
                                    }
                                    catch (Exception)
                                    {
                                        // ignored
                                    }

                                    await HandleException(ex, settings.SecurityDelay, Email);
                                    return true;
                                }
                            }
                            else
                            {
                                var sellPrice = AuctionInfo.CalculateNextBid(card.Value);
                                auctionDetails = new AuctionDetails(boughtAction.ItemData.Id,
                                    GetAuctionDuration(startedAt, settings.RunforHours, Login),
                                    CalculateBidPrice(sellPrice, settings.SellPercent), sellPrice);
                            }

                            if (auctionDetails != null)
                            {
                                await Task.Delay(settings.RmpDelay*4);
                                await _utClient.ListAuctionAsync(auctionDetails);
                            }
                        }
                        else
                        {
                            MarkErrorCard("Buy Error!");
                        }
                    }
                    else
                    {
                        MarkErrorCard("Buy Error!");
                    }
                    Credits = placeBid.Credits;
                }
                catch (ExpiredSessionException ex)
                {
                    MarkErrorCard("Relist Error!");
                    await HandleException(ex, settings.SecurityDelay, Email);
                    return false;
                }
                catch (ArgumentException ex)
                {
                    MarkErrorCard("Relist Error!");
                    await HandleException(ex, settings.SecurityDelay, Email);
                    return false;
                }
                catch (CaptchaTriggeredException ex)
                {
                    MarkErrorCard("Relist Error!");
                    await HandleException(ex, Email);
                    return false;
                }
                catch (HttpRequestException ex)
                {
                    MarkErrorCard("Relist Error!");
                    await HandleException(ex, settings.SecurityDelay, Email);
                    return false;
                }
                catch (Exception ex)
                {
                    MarkErrorCard("Relist Error!");
                    await HandleException(ex, settings.SecurityDelay, Email);
                    return false;
                }
            }
            return true;
        }

        public List<OrderCard> GetCustomerCards(uint tosell)
        {
            try
            {
                return Database.GetCustomerCards(Platform, Email, tosell);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    return Database.GetCustomerCards(Platform, Email, tosell);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    return new List<OrderCard>();
                }
            }
            catch (MySqlException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    return Database.GetCustomerCards(Platform, Email, tosell);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        return Database.GetCustomerCards(Platform, Email, tosell);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                        return new List<OrderCard>();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    return new List<OrderCard>();
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
                return new List<OrderCard>();
            }
        }

        public void MarkErrorCard(string message)
        {
            try
            {
                Database.MarkErrorCard(Platform, Email, message);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    Database.MarkErrorCard(Platform, Email, message);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
            catch (MySqlException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.MarkErrorCard(Platform, Email, message);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        Database.MarkErrorCard(Platform, Email, message);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
            }
        }

        public void MarkResetCard(long tradeId)
        {
            try
            {
                Database.MarkResetCard(Platform, Email, tradeId);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    Database.MarkResetCard(Platform, Email, tradeId);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
            catch (MySqlException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.MarkResetCard(Platform, Email, tradeId);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        Database.MarkResetCard(Platform, Email, tradeId);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
            }
        }

        public void MarkBoughtCard(long tradeId)
        {
            try
            {
                Database.MarkBoughtCard(Platform, Email, tradeId);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    Database.MarkBoughtCard(Platform, Email, tradeId);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
            catch (MySqlException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.MarkBoughtCard(Platform, Email, tradeId);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        Database.MarkBoughtCard(Platform, Email, tradeId);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
            }
        }
    }
}