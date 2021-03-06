﻿using System.Collections.Generic;
using UltimateTeam.Toolkit.Parameters;

namespace UltimateTeam.Toolkit.Models
{
    public class ItemData
    {
        public long AssetId { get; set; }

        public ushort Assists { get; set; }

        public List<Attribute> AttributeList { get; set; }

        public ushort CardSubTypeId { get; set; }

        public byte Contract { get; set; }

        public bool Dream { get; set; }

        public ushort? DiscardValue { get; set; }

        public byte Fitness { get; set; }

        public string Formation { get; set; }

        public long Id { get; set; }

        public byte InjuryGames { get; set; }

        public string InjuryType { get; set; }

        public string ItemState { get; set; }

        public string ItemType { get; set; }

        public uint LastSalePrice { get; set; }

        public uint LeagueId { get; set; }

        public ushort LifeTimeAssists { get; set; }

        public ushort Loans { get; set; }

        public List<Attribute> LifeTimeStats { get; set; }

        public byte LoyaltyBonus { get; set; }

        public byte Morale { get; set; }

        public byte Owners { get; set; }

        public ChemistryStyle PlayStyle { get; set; }

        public string PreferredPosition { get; set; }

        public byte RareFlag { get; set; }

        public byte Rating { get; set; }

        public long ResourceId { get; set; }

        public List<Attribute> StatsList { get; set; }

        public byte Suspension { get; set; }

        public uint TeamId { get; set; }

        public string Timestamp { get; set; }

        public int Training { get; set; }

        public bool Untradeable { get; set; }

        public int Pile { get; set; }

        public int Nation { get; set; }

        public long Value { get; set; }

        public string Name { get; set; }

        public string Category { get; set; }

        public uint MarketDataMaxPrice { get; set; }

        public uint MarketDataMinPrice { get; set; }

        public uint Weightrare { get; set; }

        public uint Bronze { get; set; }

        public uint Silver { get; set; }

        public uint Gold { get; set; }

        public uint Cardassetid { get; set; }

        public string Description { get; set; }

        public string Header { get; set; }

        public string Biodescription { get; set; }

        public uint Amount { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public uint Attribute { get; set; }

        public string DetailDescription { get; set; }

        public uint Year { get; set; }

        public string FieldPos { get; set; }

        public uint StadiumId { get; set; }

        public string Manufacturer { get; set; }

        public string Negotiation { get; set; }

        public string ResourceGameYear { get; set; }

        public ItemData TrainingItem { get; set; }      

        public int Capacity { get; set; }

        public int PosBonus { get; set; }
    }
}
