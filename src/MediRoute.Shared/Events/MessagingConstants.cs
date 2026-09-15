namespace MediRoute.Shared.Events;

public static class EventNames
{
    public const string HospitalCapacityChanged = "hospital.capacity.changed";
    public const string EmergencyRequestCreated = "emergency.request.created";
    public const string EmergencyRequestRouted = "emergency.request.routed";
    public const string AmbulanceLocationUpdated = "ambulance.location.updated";
}

public static class QueueNames
{
    public const string CapacityEvents = "mediroute.capacity";
    public const string EmergencyEvents = "mediroute.emergency";
    public const string NotificationEvents = "mediroute.notifications";
}
