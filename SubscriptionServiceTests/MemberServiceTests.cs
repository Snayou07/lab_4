
using Subscription_Service.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Subscription_Service.Models;
using Subscription_Service.Services;


namespace SubscriptionServiceTests
{
    public class MemberServiceTests
    {
        private readonly Mock<IMemberRepository> _repo = new();
        private readonly MemberService _service;

        public MemberServiceTests()
        {
            _service = new MemberService(_repo.Object);
        }

        /// <summary>
        /// Перевіряє, що метод GetMember повертає коректний об'єкт Member,
        /// якщо передано існуючий ID користувача.
        /// </summary>
        [Fact]
        public void GetMember_ValidId_ReturnsMember()
        {
            // Arrange
            var expectedMember = new Member 
            { 
                Id = 1, 
                Name = "Іван Петренко", 
                IsActive = true,
                SubscriptionEnd = DateTime.Now.AddDays(30)
            };
            _repo.Setup(r => r.GetById(1)).Returns(expectedMember);

            // Act
            var result = _service.GetMember(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedMember.Id, result.Id);
            Assert.Equal(expectedMember.Name, result.Name);
            Assert.True(result.IsActive);
            _repo.Verify(r => r.GetById(1), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що метод GetMember повертає null,
        /// якщо передано ID користувача, який не існує в системі.
        /// </summary>
        [Fact]
        public void GetMember_InvalidId_ReturnsNull()
        {
            // Arrange
            _repo.Setup(r => r.GetById(999)).Returns((Member?)null);

            // Act
            var result = _service.GetMember(999);

            // Assert
            Assert.Null(result);
            _repo.Verify(r => r.GetById(999), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що метод IsActive повертає true,
        /// якщо користувач існує і має статус IsActive = true.
        /// </summary>
        [Fact]
        public void IsActive_ActiveMember_ReturnsTrue()
        {
            // Arrange
            var activeMember = new Member 
            { 
                Id = 1, 
                Name = "Активний користувач", 
                IsActive = true 
            };
            _repo.Setup(r => r.GetById(1)).Returns(activeMember);

            // Act
            var result = _service.IsActive(1);

            // Assert
            Assert.True(result);
            _repo.Verify(r => r.GetById(1), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що метод IsActive повертає false,
        /// якщо користувач існує, але має статус IsActive = false.
        /// </summary>
        [Fact]
        public void IsActive_InactiveMember_ReturnsFalse()
        {
            // Arrange
            var inactiveMember = new Member 
            { 
                Id = 2, 
                Name = "Неактивний користувач", 
                IsActive = false 
            };
            _repo.Setup(r => r.GetById(2)).Returns(inactiveMember);

            // Act
            var result = _service.IsActive(2);

            // Assert
            Assert.False(result);
            _repo.Verify(r => r.GetById(2), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що метод IsActive повертає false,
        /// якщо користувач з таким ID не існує в системі.
        /// </summary>
        [Fact]
        public void IsActive_NonExistentMember_ReturnsFalse()
        {
            // Arrange
            _repo.Setup(r => r.GetById(999)).Returns((Member?)null);

            // Act
            var result = _service.IsActive(999);

            // Assert
            Assert.False(result);
            _repo.Verify(r => r.GetById(999), Times.Once);
        }
    }
}
