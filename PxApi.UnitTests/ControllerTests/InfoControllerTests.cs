using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PxApi.Controllers;
using PxApi.Models;

namespace PxApi.UnitTests.ControllerTests
{
    [TestFixture]
    public class InfoControllerTests
    {
        private InfoController _controller = null!;

        [SetUp]
        public void SetUp()
        {
            _controller = new InfoController
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        [Test]
        public void GetInfo_ReturnsOkWithApplicationNameAndVersion()
        {
            // Act
            IActionResult result = _controller.GetInfo();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            OkObjectResult okResult = (OkObjectResult)result;
            InfoResponse response = (InfoResponse)okResult.Value!;
            Assert.Multiple(() =>
            {
                Assert.That(response.Application, Is.EqualTo("PxApi"));
                Assert.That(response.Version, Is.Not.Null.And.Not.Empty);
            });
        }

        [Test]
        public void GetInfo_VersionIsNotUnknown()
        {
            // Act
            IActionResult result = _controller.GetInfo();

            // Assert
            OkObjectResult okResult = (OkObjectResult)result;
            InfoResponse response = (InfoResponse)okResult.Value!;
            Assert.That(response.Version, Is.Not.EqualTo("unknown"));
        }
    }
}
