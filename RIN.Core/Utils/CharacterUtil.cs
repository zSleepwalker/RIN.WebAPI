using System.Text.RegularExpressions;
using RIN.Core.Common;
using RIN.Core.DB;
using RIN.Core.Models;
using RIN.Core.SDB;

namespace RIN.Core.Utils
{
    public class CharacterUtil
    {
        public static CharacterVisuals CreateVisualsObj(NewCharaterColors colors, byte race, byte gender, int eyeColorId, int skinColorId, int hairColorId, int voiceSet, int headId, int hairId, int facialHairId = 0, int eyesId = 10001)
        {
            var visuals = new CharacterVisuals
            {
                race              = race,
                gender            = gender,
                skin_color        = new WebIdValueColor(skinColorId, colors.SkinColor),
                voice_set         = new WebId(voiceSet),
                head              = new WebId(headId),
                eye_color         = new WebIdValueColor(eyeColorId, colors.EyeColor),
                lip_color         = new WebIdValueColor(0, 0),
                hair_color        = new WebIdValueColor(hairColorId, colors.HairColor),
                facial_hair_color = new WebIdValueColor(hairColorId, colors.HairColor),
                head_accessories  = new List<WebIdValueColor> {new(hairId, colors.HairColor)},
                ornaments         = new List<WebId>(),
                eyes              = new WebId(eyesId),
                hair              = new WebIdValueColorId(hairId, hairColorId, colors.HairColor),
                facial_hair       = new WebIdValueColorId(facialHairId, hairColorId, colors.HairColor),
                glider            = new WebId(0),
                vehicle           = new WebId(0)
            };

            return visuals;
        }
        
        public static byte GenderStrToNum(string gender) => string.Equals(gender, "female", StringComparison.InvariantCultureIgnoreCase) ? (byte)1 : (byte)0;

        public static string GenderNumToString(int gender)
        {
            var genderStr = gender switch
            {
                0 => "male",
                1 => "female",
                _ => "male"
            };

            return genderStr;
        }

        public static string RaceIdToString(int id)
        {
            var race = id switch
            {
                0  => "human",
                2  => "dark_one",
                6  => "monster",
                7  => "friendly",
                8  => "melding",
                9  => "gaea",
                10 => "bandit",
                _  => "human"
            };

            return race;
        }

        // Game client does not allow for names to contain Control or Punctuation characters
        // Only allows a name to contain the following characters a-z, A-Z, 0-9, and ' '
        private static readonly Regex ValidChars = new Regex(@"^[a-zA-Z0-9 ]*$", RegexOptions.Compiled | RegexOptions.Singleline);

        public static bool IsInvalidCharactersInName(string name)
        {
            return !ValidChars.IsMatch(name);
        }

        public static bool IsReservedName(string name)
        {
            var reserved = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            {
                "Admin", "Administrator", "GameMaster", "GM", "System", "Support", "Red5", "Red5Studios", "Firefall"
            };
            return reserved.Contains(name);
        }

        public static bool IsNameProfane(string name)
        {
            // Basic profanity filter
            // In a real scenario, this would check against a large list or use a service
            var blocked = new[] 
            { 
                "fuck", "shit", "asshole", "nigger", "faggot", "cunt", "pussy", "dick", "cock", "bastard", "bitch" 
            };
            
            var normalized = name.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
            return blocked.Any(w => normalized.Contains(w));
        }

        public static CharacterVisuals UpdateCharacterVisualsFromGarage(
            CharacterVisuals cv,
            PlayerVisualLoadout updates,
            NewCharaterColors colors,
            IReadOnlyDictionary<int, int>? ornamentUsageById = null)
        {
            NormalizeHairVisuals(cv);

            cv.race             = updates.race;
            cv.gender           = updates.gender;
            cv.voice_set.id     = updates.voice_set_id;
            cv.head.id          = updates.head_id;
            cv.lip_color.id     = updates.lip_color_id;
            cv.facial_hair_color.id = updates.facial_hair_color_id;
            cv.eyes.id          = updates.eye_id;

            // Client treats hair/facial hair as head accessories; keep both representations in sync.
            if (cv.head_accessories.Count == 0)
            {
                cv.head_accessories.Add(new WebIdValueColor(updates.hair_id, colors.HairColor));
            }

            if (cv.skin_color.id != updates.skin_color_id)
            {
                cv.skin_color.id = updates.skin_color_id;
                cv.skin_color.value.color = colors.SkinColor;
            }

            if (cv.eye_color.id != updates.eye_color_id)
            {
                cv.eye_color.id = updates.eye_color_id;
                cv.eye_color.value.color = colors.EyeColor;
            }

            if (cv.hair.id != updates.hair_id)
            {
                cv.hair.id = updates.hair_id;
                cv.head_accessories[0].id = updates.hair_id;
            }

            if (cv.facial_hair.id != updates.facial_hair_id)
            {
                cv.facial_hair.id = updates.facial_hair_id;
                if (updates.facial_hair_id > 0)
                {
                    if (cv.head_accessories.Count > 1)
                    {
                        cv.head_accessories[1].id = updates.facial_hair_id;
                    }
                    else
                    {
                        cv.head_accessories.Add(new WebIdValueColor(updates.facial_hair_id, colors.HairColor));
                    }
                }
            }

            if (cv.hair_color.id != updates.hair_color_id)
            {
                cv.hair_color.id                  = updates.hair_color_id;
                cv.hair_color.value.color         = colors.HairColor;
                cv.head_accessories[0].value.color = colors.HairColor;
                cv.hair.color.id                  = updates.hair_color_id;
                cv.hair.color.value               = colors.HairColor;

                cv.facial_hair_color.id           = updates.facial_hair_color_id;
                if (cv.head_accessories.Count > 1)
                {
                    cv.head_accessories[1].value.color = colors.HairColor;
                }
                cv.facial_hair.color.id           = updates.facial_hair_color_id;
                cv.facial_hair.color.value        = colors.HairColor;
                cv.facial_hair_color.value.color  = colors.HairColor;
            }

            cv.ornaments = NormalizeOrnaments(updates.ornaments, ornamentUsageById);

            NormalizeHairVisuals(cv);
            return cv;
        }

        private static List<WebId> NormalizeOrnaments(IReadOnlyList<RemoteItem>? requestedOrnaments, IReadOnlyDictionary<int, int>? ornamentUsageById)
        {
            if (requestedOrnaments == null || requestedOrnaments.Count == 0)
            {
                return new List<WebId>();
            }

            // Keep the latest selected ornament for each known usage bucket (eye/ear/mouth/etc)
            // and dedupe unknown ornaments by id while preserving the latest user order.
            var byUsage = new Dictionary<int, (int Index, int Id)>();
            var byId = new Dictionary<int, int>();

            for (int index = 0; index < requestedOrnaments.Count; index++)
            {
                int ornamentId = requestedOrnaments[index]?.remote_id ?? 0;
                if (ornamentId <= 0)
                {
                    continue;
                }

                if (ornamentUsageById != null && ornamentUsageById.TryGetValue(ornamentId, out int usage))
                {
                    byUsage[usage] = (index, ornamentId);
                    continue;
                }

                byId[ornamentId] = index;
            }

            var normalized = byUsage
                .Values
                .Concat(byId.Select(pair => (Index: pair.Value, Id: pair.Key)))
                .OrderBy(pair => pair.Index)
                .Select(pair => new WebId(pair.Id))
                .ToList();

            return normalized;
        }

        public static void NormalizeHairVisuals(CharacterVisuals cv)
        {
            cv.hair ??= new WebIdValueColorId();
            cv.facial_hair ??= new WebIdValueColorId();
            cv.hair_color ??= new WebIdValueColor();
            cv.facial_hair_color ??= new WebIdValueColor();
            cv.hair_color.value ??= new WebColor();
            cv.facial_hair_color.value ??= new WebColor();
            cv.hair.color ??= new WebColorId();
            cv.facial_hair.color ??= new WebColorId();
            cv.head_accessories ??= new List<WebIdValueColor>();

            var hairId = cv.hair.id;
            if (hairId <= 0 && cv.head_accessories.Count > 0)
            {
                hairId = cv.head_accessories[0].id;
            }

            var facialHairId = cv.facial_hair.id;
            if (facialHairId <= 0 && cv.head_accessories.Count > 1)
            {
                facialHairId = cv.head_accessories[1].id;
            }

            cv.hair.id = hairId;
            cv.facial_hair.id = facialHairId;

            // Keep color IDs and packed values aligned across all representations.
            cv.hair.color.id = cv.hair_color.id;
            cv.hair.color.value = cv.hair_color.value.color;

            if (cv.facial_hair_color.id == 0)
            {
                cv.facial_hair_color.id = cv.hair_color.id;
            }

            if (cv.facial_hair_color.value.color == 0)
            {
                cv.facial_hair_color.value.color = cv.hair_color.value.color;
            }

            cv.facial_hair.color.id = cv.facial_hair_color.id;
            cv.facial_hair.color.value = cv.facial_hair_color.value.color;

            // Always derive accessory slots from hair fields to avoid stale ordering/content.
            var normalizedAccessories = new List<WebIdValueColor>(2);
            if (cv.hair.id > 0)
            {
                normalizedAccessories.Add(new WebIdValueColor(cv.hair.id, cv.hair_color.value.color));
            }

            if (cv.facial_hair.id > 0)
            {
                normalizedAccessories.Add(new WebIdValueColor(cv.facial_hair.id, cv.facial_hair_color.value.color));
            }

            cv.head_accessories = normalizedAccessories;
        }
    }
}