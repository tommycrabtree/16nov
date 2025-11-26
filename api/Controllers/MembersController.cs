using api.DTOs;
using api.Entities;
using api.Extensions;
using api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[Authorize]
public class MembersController(IMemberRepository memberRepository,
    IPhotoService photoService) : BaseApiController
{
    [HttpGet] // localhost:5001/api/members
    public async Task<ActionResult<IReadOnlyList<Member>>> GetMembers()
    {
        return Ok(await memberRepository.GetMembersAsync());
    }

    [HttpGet("{id}")] // localhost:5001/api/members/rebel-id
    public async Task<ActionResult<Member>> GetMember(string id)
    {
        var member = await memberRepository.GetMemberByIdAsync(id);

        if (member == null) return NotFound();

        return member;
    }

    [HttpGet("{id}/photos")]
    public async Task<ActionResult<IReadOnlyList<Photo>>> GetMemberPhotos(string id)
    {
        return Ok(await memberRepository.GetPhotosForMemberAsync(id));
    }

    [HttpPut]
    public async Task<ActionResult> UpdateMember(MemberUpdateDto memberUpdateDto)
    {
        var memberId = User.GetMemberId();

        var member = await memberRepository.GetMemberForUpdate(memberId);

        if (member == null) return BadRequest("Couldn't get the member");

        member.DisplayName = memberUpdateDto.DisplayName ?? member.DisplayName;
        member.Description = memberUpdateDto.Description ?? member.Description;
        member.City = memberUpdateDto.City ?? member.City;
        member.Country = memberUpdateDto.Country ?? member.Country;

        member.User.DisplayName = memberUpdateDto.DisplayName ?? member.User.DisplayName;

        // memberRepository.Update(member); // optional

        if (await memberRepository.SaveAllAsync()) return NoContent();

        return BadRequest("Couldn't update the member, buddy");
    }

    [HttpPost("add-photo")]
    public async Task<ActionResult<Photo>> AddPhoto([FromForm] IFormFile file)
    {
        var member = await memberRepository.GetMemberForUpdate(User.GetMemberId());

        if (member == null) return BadRequest("Can't update the member, bud");

        var result = await photoService.UploadPhotoAsync(file);

        if (result.Error != null) return BadRequest(result.Error.Message);

        var photo = new Photo
        {
            Url = result.SecureUrl.AbsoluteUri,
            PublicId = result.PublicId,
            MemberId = User.GetMemberId()
        };

        if (member.ImageUrl == null)
        {
            member.ImageUrl = photo.Url;
            member.User.ImageUrl = photo.Url;
        }

        member.Photos.Add(photo);

        if (await memberRepository.SaveAllAsync()) return photo;

        return BadRequest("Problemo adding photo, senor");
    }

    [HttpPut("set-main-photo/{photoid}")]
    public async Task<ActionResult> SetMainPhoto(int photoid)
    {
        var member = await memberRepository.GetMemberForUpdate(User.GetMemberId());

        if (member == null) return BadRequest("Can't get member from token, g");

        var photo = member.Photos.SingleOrDefault(x => x.Id == photoid);

        if (member.ImageUrl == photo?.Url || photo == null)
        {
            return BadRequest("Can't set this as the main image, homie");
        }

        member.ImageUrl = photo.Url;
        member.User.ImageUrl = photo.Url;

        if (await memberRepository.SaveAllAsync()) return NoContent();

        return BadRequest("Problem setting main photo, my friend");
    }

    [HttpDelete("delete-photo/{photoId}")]
    public async Task<ActionResult> DeletePhoto(int photoId)
    {
        var member = await memberRepository.GetMemberForUpdate(User.GetMemberId());

        if (member == null) return BadRequest("Can't get member from token, bud");

        var photo = member.Photos.SingleOrDefault(x => x.Id == photoId);

        if (photo == null || photo.Url == member.ImageUrl)
        {
            return BadRequest("This photo can't be deleted, my brother");
        }

        if (photo.PublicId != null)
        {
            var result = await photoService.DeletePhotoAsync(photo.PublicId);
            if (result.Error != null) return BadRequest(result.Error.Message);
        }

        member.Photos.Remove(photo);

        if (await memberRepository.SaveAllAsync()) return Ok();

        return BadRequest("Problemo deleting el photo");
    }
}
