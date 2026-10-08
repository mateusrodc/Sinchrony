using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Scheduling;

namespace Sinchrony.Application.Classes;

// type: "studio" (mesma sala) ou "teacher" (mesmo professor).
public record ClassConflictDto(string Type, Guid ClassId, string ClassName, string Date, string StartTime, string EndTime);

public static class ClassConflictChecker
{
    // `candidates` = aulas não canceladas (ClassRepository.ListSchedulingCandidatesAsync). Uma mesma
    // aula pode conflitar por sala e por professor ao mesmo tempo: aparece duas vezes.
    public static List<ClassConflictDto> Find(
        DateOnly date, string startTime, string endTime, Guid studioId, Guid teacherId,
        IEnumerable<Class> candidates, Guid? ignoreClassId = null)
    {
        var result = new List<ClassConflictDto>();
        foreach (var c in candidates)
        {
            if (c.Date != date || c.Id == ignoreClassId) continue;
            if (!ClassSchedule.Overlaps(startTime, endTime, c.StartTime, c.EndTime)) continue;

            if (c.StudioId == studioId) result.Add(Map("studio", c));
            if (c.TeacherId == teacherId) result.Add(Map("teacher", c));
        }
        return result;
    }

    private static ClassConflictDto Map(string type, Class c)
        => new(type, c.Id, c.Name, c.Date.ToString("yyyy-MM-dd"), c.StartTime, c.EndTime);
}
