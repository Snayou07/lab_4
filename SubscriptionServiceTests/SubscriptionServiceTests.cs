using Moq;
using Subscription_Service.Models;
using Subscription_Service.Services;
using Subscription_Service.Services.Interfaces;

namespace SubscriptionServiceTests
{
    public class SubscriptionServiceTests
    {
        private readonly Mock<IMemberRepository> _repo = new();
        private readonly Mock<IPaymentService> _payment = new();
        private readonly Mock<INotificationService> _notify = new();

    }

}
