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
        Assert.False(dog.IsDeleted);
    }

    [Fact]
    public void SoftDelete_SetsIsDeleted()
    {
        var dog = new Dog { Name = "Bello", Gender = DogGender.Male };

        dog.DeletedAt = DateTimeOffset.UtcNow;

        Assert.True(dog.IsDeleted);
    }
}
