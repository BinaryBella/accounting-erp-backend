using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.Api.Controllers;

[ApiController]
[Route("api/accounts")]
[Produces("application/json")]
public sealed class AccountsController : ControllerBase
{
    private readonly IAccountService _accounts;

    public AccountsController(IAccountService accounts) => _accounts = accounts;

    /// <summary>Chart of accounts, filtered and paged.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AccountResponse>), StatusCodes.Status200OK)]
    public Task<PagedResult<AccountResponse>> List(
        [FromQuery] byte? accountType,
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
        => _accounts.ListAsync(new AccountQuery(accountType, search, isActive, page, pageSize));

    [HttpGet("{id:int}", Name = "GetAccount")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<AccountResponse> Get(int id) => _accounts.GetAsync(id);

    [HttpPost]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountResponse>> Create([FromBody] CreateAccountRequest request)
    {
        var created = await _accounts.CreateAsync(request);
        return CreatedAtRoute("GetAccount", new { id = created.AccountId }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<AccountResponse> Update(int id, [FromBody] UpdateAccountRequest request)
        => _accounts.UpdateAsync(id, request);

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        await _accounts.DeleteAsync(id);
        return NoContent();
    }
}

[ApiController]
[Route("api/account-types")]
[Produces("application/json")]
public sealed class AccountTypesController : ControllerBase
{
    private readonly IAccountService _accounts;

    public AccountTypesController(IAccountService accounts) => _accounts = accounts;

    /// <summary>The five account types with their normal balance and balance-sheet flag.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AccountTypeResponse>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<AccountTypeResponse>> List() => _accounts.ListTypesAsync();
}
