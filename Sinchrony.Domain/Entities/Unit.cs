namespace Sinchrony.Domain.Entities;

public class Unit
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public bool Active { get; private set; } = true;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public string? Cep { get; private set; }
    public string? Logradouro { get; private set; }
    public string? Numero { get; private set; }
    public string? Complemento { get; private set; }
    public string? Bairro { get; private set; }
    public string? Cidade { get; private set; }
    public string? Estado { get; private set; }

    public ICollection<User> Users { get; private set; } = [];
    public ICollection<Studio> Studios { get; private set; } = [];

    protected Unit() { }

    public static Unit Create(string name, string? address = null,
        string? phone = null, string? email = null)
        => new() { Name = name, Address = address, Phone = phone, Email = email };

    public void Update(string name, string? address, string? phone, string? email, bool active)
    {
        Name = name;
        Address = address;
        Phone = phone;
        Email = email;
        Active = active;
        UpdatedAt = DateTime.UtcNow;
    }
    public void UpdateAddress(string? cep, string? logradouro, string? numero,
        string? complemento, string? bairro, string? cidade, string? estado)
    {
        Cep = string.IsNullOrEmpty(cep) ? null : cep.Replace("-", "").Trim();
        Logradouro = logradouro;
        Numero = numero;
        Complemento = complemento;
        Bairro = bairro;
        Cidade = cidade;
        Estado = estado;
        UpdatedAt = DateTime.UtcNow;
    }
}