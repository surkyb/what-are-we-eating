using WhatAreWeEating.Core.Services;

namespace WhatAreWeEating.Tests;

public class PasswordValidatorTests
{
    private readonly PasswordValidator _validator = new();

    // CASO VÁLIDO: una contraseña con letras y números, de longitud normal, se acepta.
    [Fact]
    public void Validate_ContrasenaConLetrasYNumeros_EsValida()
    {
        // Arrange
        const string password = "Abcdef123";

        // Act
        var (isValid, errorMessage) = _validator.Validate(password);

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    // BORDE: 8 caracteres exactos es el mínimo permitido, por lo que se acepta.
    [Fact]
    public void Validate_ContrasenaConExactamente8Caracteres_EsValida()
    {
        // Arrange
        const string password = "Abcdefg1";

        // Act
        var (isValid, errorMessage) = _validator.Validate(password);

        // Assert
        Assert.Equal(8, password.Length);
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    // INVÁLIDO: una contraseña sin número incumple la política y se rechaza.
    [Fact]
    public void Validate_ContrasenaSinNumero_SeRechaza()
    {
        // Arrange
        const string password = "abcdefgh";

        // Act
        var (isValid, errorMessage) = _validator.Validate(password);

        // Assert
        Assert.False(isValid);
        Assert.Equal("La contraseña debe contener al menos una letra y un número.", errorMessage);
    }

    // EXTRA (Theory): otras entradas inválidas, cada una con el mensaje que le corresponde.
    [Theory]
    [InlineData("Abcdef1", "La contraseña debe tener al menos 8 caracteres.")]
    [InlineData("12345678", "La contraseña debe contener al menos una letra y un número.")]
    [InlineData("", "La contraseña es requerida.")]
    public void Validate_ContrasenaInvalida_SeRechazaConSuMensaje(string password, string mensajeEsperado)
    {
        // Arrange (los datos llegan por InlineData)

        // Act
        var (isValid, errorMessage) = _validator.Validate(password);

        // Assert
        Assert.False(isValid);
        Assert.Equal(mensajeEsperado, errorMessage);
    }
}
