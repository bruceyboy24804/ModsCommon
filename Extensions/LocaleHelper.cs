using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Colossal;
using Colossal.Json;
using Game.SceneFlow;
using Game.UI.Localization;
using Game.UI.Tooltip;

namespace ModsCommon.Extensions
{
    public class LocaleHelper
	{
		private readonly Dictionary<string, Dictionary<string, string>> _locale;

		private static string? s_ModName;

		/// <summary>
		/// The mod name used to build localisation keys for the tooltip and action helpers.
		/// Set once per mod (e.g. <c>=&gt; LocaleHelper.Initialize(MyMod.Instance.ModName)</c>);
		/// <see cref="ModsCommon.Mod.ModsCommonBase{TSelf}"/> does this during OnLoad.
		/// </summary>
		public static void Initialize(string modName) => s_ModName = modName;

		private static string ModName => s_ModName ?? string.Empty;

		private static readonly string[] _supportedLocales =
		[
			"de-DE", "en-US", "es-ES", "fr-FR", "it-IT", "ja-JP", "ko-KR",
			"nl-NL", "pl-PL", "pt-BR", "ru-RU", "zh-HANS", "zh-HANT", "zh-HK", "zh-TW"
		];

		private static readonly Dictionary<string, string[]> _supportedCultures = new()
		{
			{ "en-US", ["nl-NL"] },
			{ "zh-HANT", ["zh-HK", "zh-TW"] }
		};

		public LocaleHelper(string dictionaryResourceName)
		{
			var assembly = GetType().Assembly;

			_locale = new Dictionary<string, Dictionary<string, string>>
			{
				[string.Empty] = GetDictionary(dictionaryResourceName)
			};

			foreach (var name in assembly.GetManifestResourceNames())
			{
				if (name == dictionaryResourceName || !name.Contains(Path.GetFileNameWithoutExtension(dictionaryResourceName) + "."))
				{
					continue;
				}

				var key = Path.GetFileNameWithoutExtension(name);

				_locale[key.Substring(key.LastIndexOf('.') + 1)] = GetDictionary(name);
			}

			Dictionary<string, string> GetDictionary(string resourceName)
			{
				using var resourceStream = assembly.GetManifestResourceStream(resourceName);
				if (resourceStream == null)
				{
					return new Dictionary<string, string>();
				}

				using var reader = new StreamReader(resourceStream, Encoding.UTF8);
				JSON.MakeInto<Dictionary<string, string>>(JSON.Load(reader.ReadToEnd()), out var dictionary);

				return dictionary;
			}
		}

		public static string? Translate(string? id, string? fallback = null)
		{
			if (id is not null && GameManager.instance.localizationManager.activeDictionary.TryGetValue(id, out var result))
			{
				return result;
			}

			return fallback ?? id;
		}

		/// <summary>
		/// Looks up a tooltip label registered as <c>Tooltip.LABEL[{ModName}.{key}]</c>.
		/// </summary>
		public static string? GetTooltip(string key)
		{
			return Translate($"Tooltip.LABEL[{ModName}.{key}]");
		}

		/// <summary>
		/// Looks up the mod's own input action label,
		/// registered as <c>Common.ACTION[{ModName}.{ModName}.Mod/{key}]</c>.
		/// </summary>
		public static string? GetAction(string key)
		{
			return Translate($"Common.ACTION[{ModName}.{ModName}.Mod/{key}]");
		}

		/// <summary>
		/// Looks up a hint-tooltip action label,
		/// registered as <c>Common.ACTION[{ModName}.HintTooltip.{type}/{key}]</c>.
		/// </summary>
		public static string? GetAction(string type, string key)
		{
			return Translate($"Common.ACTION[{ModName}.HintTooltip.{type}/{key}]");
		}

		/// <summary>
		/// Builds a <see cref="StringTooltip"/> bound to <c>Tooltip.LABEL[{ModName}.{key}]</c>,
		/// so the game resolves the text against the active locale rather than a captured string.
		/// </summary>
		public static StringTooltip GetTooltipWithIcon(string key, string? icon = null)
		{
			var path = $"Tooltip.LABEL[{ModName}.{key}]";
			return new StringTooltip { path = path, icon = icon, value = LocalizedString.Id(path) };
		}


		public static string GetLocale(string gameLocale, string cultureLocale)
		{
			if (_supportedCultures.TryGetValue(gameLocale, out var cultures) && cultures.Contains(cultureLocale))
				return cultureLocale;
			if (_supportedLocales.Contains(gameLocale))
				return gameLocale;
			return "en-US";
		}

		public IEnumerable<DictionarySource> GetAvailableLanguages()
		{
			foreach (var item in _locale)
			{
				yield return new DictionarySource(item.Key is "" ? "en-US" : item.Key, item.Value);
			}
		}

		public class DictionarySource : IDictionarySource
		{
			private readonly Dictionary<string, string> _dictionary;

			public DictionarySource(string localeId, Dictionary<string, string> dictionary)
			{
				LocaleId = localeId;
				_dictionary = dictionary;
			}

			public string LocaleId { get; }

			public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
			{
				return _dictionary;
			}

			public void Unload() { }
		}
	}
}