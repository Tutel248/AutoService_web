namespace AutoService_web.Domain;

public class Client
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public class Vehicle
{
    public int Id { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}

public class Appointment
{
    public int Id { get; set; }
    public int ServiceItemId { get; set; }
    public DateTime ScheduledTime { get; set; }
    public bool IsConfirmed { get; private set; }

    public void ConfirmAppointment()
    {
        IsConfirmed = true;
    }
}