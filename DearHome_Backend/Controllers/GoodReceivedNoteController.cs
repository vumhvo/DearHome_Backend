using AutoMapper;
using DearHome_Backend.DTOs.GoodReceivedNoteDtos;
using DearHome_Backend.Models;
using DearHome_Backend.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DearHome_Backend.Constants;
using Microsoft.AspNetCore.Authorization;

namespace DearHome_Backend.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
public class GoodReceivedNoteController : ControllerBase
{
    private readonly IGoodReceivedNoteService _goodReceivedNoteService;
    private readonly IMapper _mapper;

    public GoodReceivedNoteController(IGoodReceivedNoteService goodReceivedNoteService, IMapper mapper)
    {
        _goodReceivedNoteService = goodReceivedNoteService;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var goodReceivedNotes = await _goodReceivedNoteService.GetAllAsync();
        var goodReceivedNoteDtos = _mapper.Map<List<GoodReceivedNoteDto>>(goodReceivedNotes);
        return Ok(goodReceivedNoteDtos);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var goodReceivedNote = await _goodReceivedNoteService.GetByIdAsync(id);
        if (goodReceivedNote == null)
        {
            return NotFound();
        }
        var goodReceivedNoteDto = _mapper.Map<GoodReceivedNoteDto>(goodReceivedNote);
        return Ok(goodReceivedNoteDto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AddGoodReceivedNoteDto addGoodReceivedNoteDto)
    {
        var goodReceivedNote = _mapper.Map<GoodReceivedNote>(addGoodReceivedNoteDto);
        await _goodReceivedNoteService.CreateAsync(goodReceivedNote);
        return Ok("Good Received Note created successfully.");
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateGoodReceivedNoteDto updateGoodReceivedNoteDto)
    {
        var goodReceivedNote = _mapper.Map<GoodReceivedNote>(updateGoodReceivedNoteDto);
        await _goodReceivedNoteService.UpdateAsync(goodReceivedNote);
        return Ok("Good Received Note updated successfully.");
    }
}
