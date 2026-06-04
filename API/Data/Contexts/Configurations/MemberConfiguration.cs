using System;
using API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API.Data.Contexts.Configurations;

public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("Members");


        builder.HasKey(m => m.Id);

       
        // =========================
        // RELATIONS 
        // =========================

        // member -> Monada
        builder.HasOne(m => m.Monada)
               .WithMany(i => i.Members)
               .HasForeignKey(m => m.MonadaId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}