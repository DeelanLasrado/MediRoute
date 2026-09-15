namespace MediRoute.Shared.Enums;

public enum RequestStatus
{
    Pending = 0,
    Triaged = 1,
    Routed = 2,
    EnRoute = 3,
    Arrived = 4,
    Completed = 5,
    Cancelled = 6
}
