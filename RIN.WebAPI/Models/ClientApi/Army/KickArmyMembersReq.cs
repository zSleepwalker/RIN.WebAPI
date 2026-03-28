using System.ComponentModel.DataAnnotations;

namespace RIN.WebAPI.Models.ClientApi;

public class KickArmyMembersReq
{
    [Required] public string[] character_guids { get; set; } = System.Array.Empty<string>();

    public long[] CharacterGuids => character_guids.Select(long.Parse).ToArray();
}
