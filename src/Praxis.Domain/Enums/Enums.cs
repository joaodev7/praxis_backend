namespace Praxis.Domain.Enums;

public enum UserRole
{
    PraxisAdmin = 1,
    TenantAdmin = 2,
    Nutritionist = 3,
    ClientUser = 4
}

public enum UserStatus
{
    Active = 1,
    Inactive = 2,
    Blocked = 3
}

public enum TenantStatus
{
    Active = 1,
    Inactive = 2,
    Suspended = 3
}

public enum CommonStatus
{
    Active = 1,
    Inactive = 2
}

public enum ArtStatus
{
    Active = 1,
    Suspended = 2,
    Ended = 3,
    Expired = 4
}

public enum VisitStatus
{
    Scheduled = 1,
    InProgress = 2,
    Finished = 3,
    Cancelled = 4
}

public enum EvaluationResult
{
    Conforme = 1,
    NaoConforme = 2,
    NaoAplicavel = 3
}

public enum NonConformitySeverity
{
    Baixa = 1,
    Media = 2,
    Alta = 3,
    Critica = 4
}

public enum NonConformityStatus
{
    Aberta = 1,
    EmAndamento = 2,
    Resolvida = 3,
    Cancelada = 4
}

public enum ActionItemStatus
{
    Pendente = 1,
    EmAndamento = 2,
    Concluida = 3,
    Cancelada = 4
}

public enum EvidenceType
{
    Photo = 1,
    Document = 2,
    Note = 3
}
