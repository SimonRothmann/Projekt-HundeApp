using Dogity.Domain.Dogs;
using Xunit;

namespace Dogity.Domain.Tests;

public class DogTests
{
    [Fact]
    public void NewDog_HasGeneratedId_AndIsNotDeleted()
    {
        var dog = new Dog { Name = "Bello", Gender = DogGender.Male };

        Assert.NotEqual(Guid.Empty, dog.Id);
        Assert.Null(dog.DeletedAt);
    }

    [Fact]
    public void EveryNewDog_GetsItsOwnId()
    {
        var first = new Dog { Name = "Bello" };
        var second = new Dog { Name = "Bello" };

        Assert.NotEqual(first.Id, second.Id);
    }
}
