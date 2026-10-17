namespace Biblioteka.Models { 
    public enum BookStatus {
        Available,
        Reserved,
        Borrowed,
        Maintenance,
        Lost
    }

    public enum ReservationStatus {
        Pending,
        Fulfilled,
        Cancelled,
        Expired
    }
}