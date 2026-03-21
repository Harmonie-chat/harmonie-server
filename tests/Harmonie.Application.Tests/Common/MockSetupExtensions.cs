using Harmonie.Application.Interfaces.Common;
using Moq;

namespace Harmonie.Application.Tests.Common;

internal static class MockSetupExtensions
{
    /// <summary>
    /// Creates a transaction mock, wires it to the unit-of-work mock, and returns the
    /// transaction mock so the caller can verify it later.
    /// </summary>
    public static Mock<IUnitOfWorkTransaction> SetupTransactionMock(this Mock<IUnitOfWork> unitOfWorkMock)
    {
        var transactionMock = new Mock<IUnitOfWorkTransaction>();

        unitOfWorkMock
            .Setup(x => x.BeginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactionMock.Object);

        transactionMock
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        transactionMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        return transactionMock;
    }
}
