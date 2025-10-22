using Moq;
using Subscription_Service.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SubscriptionServiceTests
{
    public class MemberServiceTests
    {
        private readonly Mock<IMemberRepository> _repo = new();
        private readonly Mock<IPaymentService> _payment = new();
        private readonly Mock<INotificationService> _notify = new();
    }
}
