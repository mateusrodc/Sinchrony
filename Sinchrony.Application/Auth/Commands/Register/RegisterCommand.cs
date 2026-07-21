using MediatR;
using Sinchrony.Application.Auth.Commands.Login;

namespace Sinchrony.Application.Auth.Commands.Register;

public record RegisterCommand(string Name, string Email, string? Phone, string Password, string? Cpf, string? cep, string? logradouro, string? numero,
    string? complemento, string? bairro, string? cidade, string? estado) : IRequest<AuthResponseDto>;