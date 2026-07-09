namespace Sinchrony.Domain.Entities;

public class ClassType
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public bool Active { get; private set; } = true;
    public bool UsesBikes { get; private set; }

    public ICollection<Class> Classes { get; private set; } = [];

    protected ClassType() { }

    public static ClassType Create(string name) => new() { Name = name };
    public void Update(string name, bool active, bool usesBikes)
    {
        Name = name;
        Active = active;
        UsesBikes = usesBikes;
    }
}