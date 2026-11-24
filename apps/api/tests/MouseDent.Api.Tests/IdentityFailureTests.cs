using Microsoft.AspNetCore.Identity;
using MouseDent.Infrastructure.Auth;
using Shouldly;

namespace MouseDent.Api.Tests;

/// <summary>
/// Traducción de los fallos de Identity. El nombre de usuario es un identificador
/// interno, así que el único fallo esperable por unicidad es el correo duplicado; un
/// <c>DuplicateUserName</c> es un error de programación y no debe pasar como correo
/// ocupado.
/// </summary>
public sealed class IdentityFailureTests
{
    [Fact]
    public void Only_a_duplicate_email_is_reported_as_a_taken_email()
    {
        IdentityFailure.IsDuplicateEmail(Failure("DuplicateEmail")).ShouldBeTrue();
        IdentityFailure.IsDuplicateEmail(Failure("DuplicateUserName")).ShouldBeFalse();
        IdentityFailure.IsDuplicateEmail(Failure("PasswordTooShort")).ShouldBeFalse();
    }

    private static IdentityResult Failure(string code) =>
        IdentityResult.Failed(new IdentityError { Code = code, Description = code });
}
