using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.VmManagement.Wmi;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using LanguageExt;
using LanguageExt.SomeHelp;
using Xunit;

using static Eryph.VmManagement.Wmi.WmiUtils;
using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Test.Wmi;

public class WmiUtilsTests
{
    private const string PropertyName = "TestProperty";

    [Fact]
    public void GetRequiredValue_ValueIsNotNull_ReturnsValue()
    {
        var wmiObject = new WmiObject(HashMap(
            (PropertyName, Optional<object>(42))));

        var result = getRequiredValue<int>(wmiObject, PropertyName).Run();
        
        result.Should().BeSuccess().Which.Should().Be(42);
    }

    [Fact]
    public void GetRequiredValue_ValueIsNull_ReturnsError()
    {
        var wmiObject = new WmiObject(HashMap(
            (PropertyName, Optional<object>(null))));

        var result = getRequiredValue<string>(wmiObject, PropertyName).Run();
        
        result.Should().BeFail()
            .Which.Message.Should().Be($"The property '{PropertyName} is null.");
    }

    [Fact]
    public void GetValue_ValueIsNotNull_ReturnsValue()
    {
        var wmiObject = new WmiObject(HashMap(
            (PropertyName, Optional<object>(42))));

        var result = getValue<int>(wmiObject, PropertyName).Run();
        
        result.Should().BeSuccess()
            .Which.Should().BeSome()
            .Which.Should().Be(42);
    }

    [Fact]
    public void GetValue_ValueIsValidEnumValue_ReturnsValue()
    {
        var wmiObject = new WmiObject(HashMap(
            (PropertyName, Optional<object>(100))));

        var result = getValue<TestEnum>(wmiObject, PropertyName).Run();
        
        result.Should().BeSuccess()
            .Which.Should().BeSome()
            .Which.Should().Be(TestEnum.TestValue);
    }

    [Fact]
    public void GetValue_ValueIsValidEnumName_ReturnsValue()
    {
        var wmiObject = new WmiObject(HashMap(
            (PropertyName, Optional<object>("TestValue"))));

        var result = getValue<TestEnum>(wmiObject, PropertyName).Run();
        
        result.Should().BeSuccess()
            .Which.Should().BeSome()
            .Which.Should().Be(TestEnum.TestValue);
    }

    [Fact]
    public void GetValue_ValueIsNull_ReturnsNone()
    {
        var wmiObject = new WmiObject(HashMap(
            (PropertyName, Optional<object>(null))));

        var result = getValue<string>(wmiObject, PropertyName).Run();
        result.Should().BeSuccess()
            .Which.Should().BeNone();
    }

    [Fact]
    public void GetValue_ValueIsInvalidEnumValue_ReturnsValue()
    {
        var wmiObject = new WmiObject(HashMap(
            (PropertyName, Optional<object>(404))));

        var result = getValue<TestEnum>(wmiObject, PropertyName).Run();

        var error = result.Should().BeFail().Subject;
        error.Message.Should().Be($"The value '404' of property '{PropertyName}' is invalid.");
        error.Inner.Should().BeSome()
            .Which.Message.Should().Be("The value '404' is not valid for TestEnum.");
    }

    [Fact]
    public void GetValue_ValueIsInvalidEnumName_ReturnsValue()
    {
        var wmiObject = new WmiObject(HashMap(
            (PropertyName, Optional<object>("InvalidValue"))));

        var result = getValue<TestEnum>(wmiObject, PropertyName).Run();

        var error = result.Should().BeFail().Subject;
        error.Message.Should().Be($"The value 'InvalidValue' of property '{PropertyName}' is invalid.");
        error.Inner.Should().BeSome()
            .Which.Message.Should().Be("The value 'InvalidValue' is not valid for TestEnum.");
    }

    [Fact]
    public void GetValue_ValueHasInvalidType_ReturnsValue()
    {
        var wmiObject = new WmiObject(HashMap(
            (PropertyName, Optional<object>("InvalidValue"))));

        var result = getValue<int>(wmiObject, PropertyName).Run();

        var error = result.Should().BeFail().Subject;
        error.Message.Should().Be($"The value 'InvalidValue' of property '{PropertyName}' is invalid.");
        error.Inner.Should().BeSome()
            .Which.Message.Should().Be("The value 'InvalidValue' is not of type Int32.");
    }

    [Fact]
    public void GetValue_PropertyIsMissing_ReturnsError()
    {
        var wmiObject = new WmiObject(HashMap(
            (PropertyName, Optional<object>(null))));

        var result = getValue<string>(wmiObject, "OtherProperty").Run();
        result.Should().BeFail()
            .Which.Message.Should().Be("The property 'OtherProperty' does not exist in the WMI object.");
    }

    private enum TestEnum
    {
        TestValue = 100,
    }
}
