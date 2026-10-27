using FluentMigrator;

namespace VehicleVision.PleasanterTools.AtomFeed.Data.Migrations;

/// <summary>フィードに出す記事の範囲（更新が今日から何日前までか・最大何件か）。</summary>
/// <remarks>
/// 列が <c>NULL</c> の側は<b>無制限</b>。新しいフィードの既定（過去 90 日・100 件）は、作成時にアプリが入れる。
/// </remarks>
[Migration(13, "フィードの記事の範囲")]
public sealed class M0013_FeedRange : Migration
{
    public override void Up()
    {
        Alter.Table("Feeds")
            .AddColumn("RangePastDays").AsInt32().Nullable()
            .AddColumn("RangeMaxItems").AsInt32().Nullable();
    }

    public override void Down()
    {
        Delete.Column("RangeMaxItems").FromTable("Feeds");
        Delete.Column("RangePastDays").FromTable("Feeds");
    }
}
