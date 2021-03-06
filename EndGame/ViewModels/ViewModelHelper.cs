﻿using HDT.Plugins.Common.Enums;
using HDT.Plugins.Common.Models;
using HDT.Plugins.Common.Services;
using HDT.Plugins.EndGame.Models;
using HDT.Plugins.EndGame.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace HDT.Plugins.EndGame.ViewModels
{
	public static class ViewModelHelper
	{
		public static void FocusTextBox(TextBox box)
		{
			EndGame.Logger.Debug($"Helper: Focusing TextBox ({box.Name})");
			box.Focus();
			if (!string.IsNullOrEmpty(box.Text) && box.CaretIndex <= 0)
			{
				EndGame.Logger.Debug($"Helper: Moving caret to ({box.Text.Length})");
				box.CaretIndex = box.Text.Length;
			}
		}

		public static List<MatchResult> MatchArchetypes(GameFormat format, Deck deck, List<ArchetypeDeck> archetypes)
		{
			EndGame.Logger.Debug($"VMHelper: {archetypes.Count()} archeytpe decks available");
			var byFormatAndClass = archetypes.Where(a =>
				(a.Class == deck.Class || deck.Class == PlayerClass.ALL)
				// archetype must be standard if in standard, if its wild all ok
				&& (format == GameFormat.STANDARD && a.IsStandard || format == GameFormat.WILD));
			EndGame.Logger.Debug($"VMHelper: {byFormatAndClass.Count()} relevant archetypes");
			return byFormatAndClass
				.Select(a => new MatchResult(a, deck.Similarity(a), a.Similarity(deck)))
				.OrderByDescending(r => r.Similarity).ThenBy(r => r.Deck.Name)
				.ToList();
		}

		public static void EditNewArchetypeDeck(IEnumerable<Card> cards)
		{
			EndGame.Logger.Debug($"VMHelper: New archetype deck edit ({cards.Count()})");
			EndGame.Client.OpenDeckEditor(cards, Strings.ArchetypeTag);
			EndGame.Client.ActivateMainWindow();
		}

		public static bool IsDeckAvailable()
		{
			var deck = EndGame.Data.GetOpponentDeck();
			return deck != null && deck.Cards.Count > 0 && deck.Class != PlayerClass.ALL;
		}

		public static bool IsModeEnabledForArchetypes(string mode)
		{
			switch (mode.ToLowerInvariant())
			{
				case "ranked":
					return EndGame.Settings.Get(Strings.RecordRankedArchetypes).Bool;

				case "casual":
					return EndGame.Settings.Get(Strings.RecordCasualArchetypes).Bool;

				case "brawl":
					return EndGame.Settings.Get(Strings.RecordBrawlArchetypes).Bool;

				case "friendly":
					return EndGame.Settings.Get(Strings.RecordFriendlyArchetypes).Bool;

				case "arena":
					return EndGame.Settings.Get(Strings.RecordArenaArchetypes).Bool;

				default:
					return EndGame.Settings.Get(Strings.RecordOtherArchetypes).Bool;
			}
		}

		public static bool ModeIsEffectedByRank(string mode)
		{
			if (mode.ToLower() == "ranked")
				return IsRankAboveThreshold();
			else
				return true;
		}

		public static bool IsRankAboveThreshold()
		{
			return EndGame.Data.GetPlayerRank() <= EndGame.Settings.Get(Strings.StartRank).Int;
		}

		public static IEnumerable<Deck> GetDecksWithArchetypeGames(IDataRepository data)
		{
			var games = data.GetAllGames()
				.Where(g => g.Note.HasArchetype)
				.Select(g => g.Deck.Id)
				.Distinct()
				.ToLookup(x => x);
			var decks = data.GetAllDecks()
				.Where(d => games.Contains(d.Id))
				.OrderBy(d => d.Name);
			return decks;
		}

		public static IEnumerable<ArchetypeRecord> GetArchetypeStats(List<Game> games)
		{
			var stats = new List<ArchetypeRecord>();
			var lookup = new Dictionary<string, ArchetypeRecord>();

			foreach (var g in games)
			{
				// if opponent class is missing skip game
				if (g.OpponentClass == PlayerClass.ALL)
				{
					EndGame.Logger.Debug($"Skipping game {g.Id}, no opponent class");
					continue;
				}
				// create an index for the archetype including class
				string index = string.Empty;
				string name = ArchetypeRecord.DefaultName;
				if (g.Note.HasArchetype)
					name = g.Note.Archetype;
				else if (!EndGame.Settings.Get(Strings.IncludeUnknown).Bool)
					// if we don't want to include unknown archetypes skip game
					continue;
				index = $"{name}.{g.OpponentClass}";
				// update an existing record or add a new one
				if (!lookup.ContainsKey(index))
					lookup[index] = new ArchetypeRecord(name, g.OpponentClass);
				lookup[index].Update(g.Result);
			}
			if (lookup.Count > 0)
				stats.AddRange(lookup.Values.ToList());

			return stats;
		}
	}
}