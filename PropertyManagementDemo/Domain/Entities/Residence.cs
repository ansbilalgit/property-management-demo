using Domain.Exceptions;

namespace Domain.Entities
{
    public class Residence
    {
        private Residence()
        {
        }

        // Created and changed only through RentalApplication.
        internal Residence(string address, string landlordName, string landlordPhone, DateOnly moveIn, DateOnly moveOut)
        {
            Apply(address, landlordName, landlordPhone, moveIn, moveOut);
        }

        public int Id { get; private set; }

        public int RentalApplicationId { get; private set; }
        public RentalApplication RentalApplication { get; private set; } = null!;

        public string Address { get; private set; } = string.Empty;
        public string LandlordName { get; private set; } = string.Empty;
        public string LandlordPhone { get; private set; } = string.Empty;
        public DateOnly MoveInDate { get; private set; }
        public DateOnly MoveOutDate { get; private set; }

        internal void Update(string address, string landlordName, string landlordPhone, DateOnly moveIn, DateOnly moveOut) =>
            Apply(address, landlordName, landlordPhone, moveIn, moveOut);

        private void Apply(string address, string landlordName, string landlordPhone, DateOnly moveIn, DateOnly moveOut)
        {
            if (moveOut < moveIn)
                throw new BusinessRuleException("MoveOutDate", "Move-out date cannot be before the move-in date.");

            Address = address.Trim();
            LandlordName = landlordName.Trim();
            LandlordPhone = landlordPhone.Trim();
            MoveInDate = moveIn;
            MoveOutDate = moveOut;
        }
    }
}
