using FluentAssertions;
using StockDesk.Application.Abstractions;
using StockDesk.Application.Reports;

namespace StockDesk.UnitTests.Services;

public sealed class CsvWriterTests
{
    [Fact]
    public void Writes_header_and_rows_with_crlf_and_invariant_formats()
    {
        var csv = CsvWriter.Write(["Date", "Revenue", "Bills"],
            [[new DateTime(2026, 11, 5), 1234.5m, 3]]);

        csv.Should().Be("Date,Revenue,Bills\r\n2026-11-05,1234.50,3\r\n");
    }

    [Fact]
    public void Quotes_fields_with_commas_quotes_and_newlines()
    {
        var csv = CsvWriter.Write(["Name"], [["Pen, blue"], ["5\" ruler"], ["two\nlines"]]);

        csv.Should().Be("Name\r\n\"Pen, blue\"\r\n\"5\"\" ruler\"\r\n\"two\nlines\"\r\n");
    }

    [Theory]
    [InlineData("=SUM(A1:A9)")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("@cmd")]
    public void Neutralises_spreadsheet_formulas(string value)
    {
        var csv = CsvWriter.Write(["Name"], [[value]]);

        csv.Should().Be("Name\r\n'" + value + "\r\n");
    }

    [Fact]
    public void Empty_and_null_fields_are_empty()
    {
        CsvWriter.Write(["A", "B"], [[null, ""]]).Should().Be("A,B\r\n,\r\n");
    }

    [Fact]
    public void Header_only_when_there_are_no_rows()
    {
        CsvWriter.Write(["A"], []).Should().Be("A\r\n");
    }
}

public sealed class SalesTrendTests
{
    [Fact]
    public void Fills_quiet_days_with_zero_and_keeps_every_day_in_range()
    {
        var rows = new[]
        {
            new DailySalesRow(new DateTime(2026, 11, 10), 2, 5, 150m),
            new DailySalesRow(new DateTime(2026, 11, 12), 1, 1, 40m),
        };

        var points = SalesTrend.Fill(rows, new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 13));

        points.Select(p => (p.Date.Day, p.Bills, p.Revenue)).Should().Equal(
            (9, 0, 0m), (10, 2, 150m), (11, 0, 0m), (12, 1, 40m), (13, 0, 0m));
    }

    [Fact]
    public void Empty_input_gives_a_flat_series()
    {
        var points = SalesTrend.Fill([], new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 14));

        points.Should().HaveCount(14).And.OnlyContain(p => p.Revenue == 0m && p.Bills == 0);
    }

    [Fact]
    public void Rejects_a_reversed_range()
    {
        var act = () => SalesTrend.Fill([], new DateOnly(2026, 11, 14), new DateOnly(2026, 11, 1));

        act.Should().Throw<ArgumentException>();
    }
}
