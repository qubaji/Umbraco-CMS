// Copyright (c) Umbraco.
// See LICENSE for more details.

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Tests.Common.Builders;
using Umbraco.Cms.Tests.Common.Builders.Extensions;

namespace Umbraco.Cms.Tests.UnitTests.Umbraco.Core.Models
{
    [TestFixture]
    public class ContentTypeTests
    {
        [Test]
        [Ignore("Ignoring this test until we actually enforce this, see comments in ContentTypeBase.PropertyTypesChanged")]
        public void Cannot_Add_Duplicate_Property_Aliases()
        {
            ContentType contentType = BuildContentType();

            var propertyTypeBuilder = new PropertyTypeBuilder();
            PropertyType additionalPropertyType = propertyTypeBuilder
                .WithAlias("title")
                .Build();

            Assert.Throws<InvalidOperationException>(() =>
                contentType.PropertyTypeCollection.Add(additionalPropertyType));
        }

        [Test]
        [Ignore("Ignoring this test until we actually enforce this, see comments in ContentTypeBase.PropertyTypesChanged")]
        public void Cannot_Update_Duplicate_Property_Aliases()
        {
            ContentType contentType = BuildContentType();

            var propertyTypeBuilder = new PropertyTypeBuilder();
            PropertyType additionalPropertyType = propertyTypeBuilder
                .WithAlias("title")
                .Build();

            contentType.PropertyTypeCollection.Add(additionalPropertyType);

            IPropertyType toUpdate = contentType.PropertyTypeCollection["myPropertyType2"];

            Assert.Throws<InvalidOperationException>(() => toUpdate.Alias = "myPropertyType");
        }

        [Test]
        public void Can_Deep_Clone_Content_Type_Sort()
        {
            ContentTypeSort contentType = BuildContentTypeSort();
            var clone = (ContentTypeSort)contentType.DeepClone();
            Assert.AreNotSame(clone, contentType);
            Assert.AreEqual(clone, contentType);
            Assert.AreEqual(clone.Id.Value, contentType.Id.Value);
            Assert.AreEqual(clone.SortOrder, contentType.SortOrder);
            Assert.AreEqual(clone.Alias, contentType.Alias);
        }

        private ContentTypeSort BuildContentTypeSort()
        {
            var builder = new ContentTypeSortBuilder();
            return builder
                .WithId(3)
                .WithSortOrder(4)
                .WithAlias("test")
                .Build();
        }

        [Test]
        public void Can_Deep_Clone_Content_Type_With_Reset_Identities()
        {
            ContentType contentType = BuildContentType();

            var clone = (ContentType)contentType.DeepCloneWithResetIdentities("newAlias");

            Assert.AreEqual("newAlias", clone.Alias);
            Assert.AreNotEqual("newAlias", contentType.Alias);
            Assert.IsFalse(clone.HasIdentity);

            foreach (PropertyGroup propertyGroup in clone.PropertyGroups)
            {
                Assert.IsFalse(propertyGroup.HasIdentity);
                foreach (IPropertyType propertyType in propertyGroup.PropertyTypes)
                {
                    Assert.IsFalse(propertyType.HasIdentity);
                }
            }

            foreach (IPropertyType propertyType in clone.PropertyTypes.Where(x => x.HasIdentity))
            {
                Assert.IsFalse(propertyType.HasIdentity);
            }
        }

        [Test]
        public void Can_Deep_Clone_Content_Type()
        {
            // Arrange
            ContentType contentType = BuildContentType();

            // Act
            var clone = (ContentType)contentType.DeepClone();

            // Assert
            Assert.AreNotSame(clone, contentType);
            Assert.AreEqual(clone, contentType);
            Assert.AreEqual(clone.Id, contentType.Id);
            Assert.AreEqual(clone.AllowedTemplates.Count(), contentType.AllowedTemplates.Count());
            for (var index = 0; index < contentType.AllowedTemplates.Count(); index++)
            {
                Assert.AreNotSame(clone.AllowedTemplates.ElementAt(index), contentType.AllowedTemplates.ElementAt(index));
                Assert.AreEqual(clone.AllowedTemplates.ElementAt(index), contentType.AllowedTemplates.ElementAt(index));
            }

            Assert.AreNotSame(clone.PropertyGroups, contentType.PropertyGroups);
            Assert.AreEqual(clone.PropertyGroups.Count, contentType.PropertyGroups.Count);
            for (var index = 0; index < contentType.PropertyGroups.Count; index++)
            {
                Assert.AreNotSame(clone.PropertyGroups[index], contentType.PropertyGroups[index]);
                Assert.AreEqual(clone.PropertyGroups[index], contentType.PropertyGroups[index]);
            }

            Assert.AreNotSame(clone.PropertyTypes, contentType.PropertyTypes);
            Assert.AreEqual(clone.PropertyTypes.Count(), contentType.PropertyTypes.Count());
            Assert.AreEqual(0, clone.NoGroupPropertyTypes.Count());
            for (var index = 0; index < contentType.PropertyTypes.Count(); index++)
            {
                Assert.AreNotSame(clone.PropertyTypes.ElementAt(index), contentType.PropertyTypes.ElementAt(index));
                Assert.AreEqual(clone.PropertyTypes.ElementAt(index), contentType.PropertyTypes.ElementAt(index));
            }

            Assert.AreEqual(clone.CreateDate, contentType.CreateDate);
            Assert.AreEqual(clone.CreatorId, contentType.CreatorId);
            Assert.AreEqual(clone.Key, contentType.Key);
            Assert.AreEqual(clone.Level, contentType.Level);
            Assert.AreEqual(clone.Path, contentType.Path);
            Assert.AreEqual(clone.SortOrder, contentType.SortOrder);
            Assert.AreNotSame(clone.DefaultTemplate, contentType.DefaultTemplate);
            Assert.AreEqual(clone.DefaultTemplate, contentType.DefaultTemplate);
            Assert.AreEqual(clone.DefaultTemplateId, contentType.DefaultTemplateId);
            Assert.AreEqual(clone.Trashed, contentType.Trashed);
            Assert.AreEqual(clone.UpdateDate, contentType.UpdateDate);
            Assert.AreEqual(clone.Thumbnail, contentType.Thumbnail);
            Assert.AreEqual(clone.Icon, contentType.Icon);
            Assert.AreEqual(clone.IsContainer, contentType.IsContainer);

            // This double verifies by reflection
            PropertyInfo[] allProps = clone.GetType().GetProperties();
            foreach (PropertyInfo propertyInfo in allProps)
            {
                Assert.AreEqual(propertyInfo.GetValue(clone, null), propertyInfo.GetValue(contentType, null));
            }

            // Need to ensure the event handlers are wired
            var asDirty = (ICanBeDirty)clone;

            Assert.IsFalse(asDirty.IsPropertyDirty("PropertyTypes"));

            var propertyTypeBuilder = new PropertyTypeBuilder();
            PropertyType additionalPropertyType = propertyTypeBuilder
                .WithAlias("blah")
                .Build();

            clone.AddPropertyType(additionalPropertyType);
            Assert.IsTrue(asDirty.IsPropertyDirty("PropertyTypes"));
            Assert.IsFalse(asDirty.IsPropertyDirty("PropertyGroups"));
            clone.AddPropertyGroup("hello", "hello");
            Assert.IsTrue(asDirty.IsPropertyDirty("PropertyGroups"));
        }

        [Test]
        public void Can_Serialize_Content_Type_Without_Error()
        {
            // Arrange
            ContentType contentType = BuildContentType();

            var json = JsonConvert.SerializeObject(contentType);
            Debug.Print(json);
        }

        private static ContentType BuildContentType()
        {
            var builder = new ContentTypeBuilder();
            return builder.BuildSimpleContentType();
        }

        [Test]
        public void Can_Deep_Clone_Media_Type()
        {
            // Arrange
            MediaType contentType = BuildMediaType();

            // Act
            var clone = (MediaType)contentType.DeepClone();

            // Assert
            Assert.AreNotSame(clone, contentType);
            Assert.AreEqual(clone, contentType);
            Assert.AreEqual(clone.Id, contentType.Id);
            Assert.AreEqual(clone.PropertyGroups.Count, contentType.PropertyGroups.Count);
            for (var index = 0; index < contentType.PropertyGroups.Count; index++)
            {
                Assert.AreNotSame(clone.PropertyGroups[index], contentType.PropertyGroups[index]);
                Assert.AreEqual(clone.PropertyGroups[index], contentType.PropertyGroups[index]);
            }

            Assert.AreEqual(clone.PropertyTypes.Count(), contentType.PropertyTypes.Count());
            for (var index = 0; index < contentType.PropertyTypes.Count(); index++)
            {
                Assert.AreNotSame(clone.PropertyTypes.ElementAt(index), contentType.PropertyTypes.ElementAt(index));
                Assert.AreEqual(clone.PropertyTypes.ElementAt(index), contentType.PropertyTypes.ElementAt(index));
            }

            Assert.AreEqual(clone.CreateDate, contentType.CreateDate);
            Assert.AreEqual(clone.CreatorId, contentType.CreatorId);
            Assert.AreEqual(clone.Key, contentType.Key);
            Assert.AreEqual(clone.Level, contentType.Level);
            Assert.AreEqual(clone.Path, contentType.Path);
            Assert.AreEqual(clone.SortOrder, contentType.SortOrder);
            Assert.AreEqual(clone.Trashed, contentType.Trashed);
            Assert.AreEqual(clone.UpdateDate, contentType.UpdateDate);
            Assert.AreEqual(clone.Thumbnail, contentType.Thumbnail);
            Assert.AreEqual(clone.Icon, contentType.Icon);
            Assert.AreEqual(clone.IsContainer, contentType.IsContainer);

            // This double verifies by reflection
            PropertyInfo[] allProps = clone.GetType().GetProperties();
            foreach (PropertyInfo propertyInfo in allProps)
            {
                Assert.AreEqual(propertyInfo.GetValue(clone, null), propertyInfo.GetValue(contentType, null));
            }
        }

        [Test]
        public void Can_Serialize_Media_Type_Without_Error()
        {
            // Arrange
            MediaType contentType = BuildMediaType();

            var json = JsonConvert.SerializeObject(contentType);
            Debug.Print(json);
        }

        private static MediaType BuildMediaType()
        {
            var builder = new MediaTypeBuilder();
            return builder.BuildImageMediaType();
        }

        [Test]
        public void Can_Deep_Clone_Member_Type()
        {
            // Arrange
            MemberType contentType = BuildMemberType();

            // Act
            var clone = (MemberType)contentType.DeepClone();

            // Assert
            Assert.AreNotSame(clone, contentType);
            Assert.AreEqual(clone, contentType);
            Assert.AreEqual(clone.Id, contentType.Id);
            Assert.AreEqual(clone.PropertyGroups.Count, contentType.PropertyGroups.Count);
            for (var index = 0; index < contentType.PropertyGroups.Count; index++)
            {
                Assert.AreNotSame(clone.PropertyGroups[index], contentType.PropertyGroups[index]);
                Assert.AreEqual(clone.PropertyGroups[index], contentType.PropertyGroups[index]);
            }

            Assert.AreEqual(clone.PropertyTypes.Count(), contentType.PropertyTypes.Count());
            for (var index = 0; index < contentType.PropertyTypes.Count(); index++)
            {
                Assert.AreNotSame(clone.PropertyTypes.ElementAt(index), contentType.PropertyTypes.ElementAt(index));
                Assert.AreEqual(clone.PropertyTypes.ElementAt(index), contentType.PropertyTypes.ElementAt(index));
            }

            Assert.AreEqual(clone.CreateDate, contentType.CreateDate);
            Assert.AreEqual(clone.CreatorId, contentType.CreatorId);
            Assert.AreEqual(clone.Key, contentType.Key);
            Assert.AreEqual(clone.Level, contentType.Level);
            Assert.AreEqual(clone.Path, contentType.Path);
            Assert.AreEqual(clone.SortOrder, contentType.SortOrder);
            Assert.AreEqual(clone.Trashed, contentType.Trashed);
            Assert.AreEqual(clone.UpdateDate, contentType.UpdateDate);
            Assert.AreEqual(clone.Thumbnail, contentType.Thumbnail);
            Assert.AreEqual(clone.Icon, contentType.Icon);
            Assert.AreEqual(clone.IsContainer, contentType.IsContainer);

            // This double verifies by reflection
            PropertyInfo[] allProps = clone.GetType().GetProperties();
            foreach (PropertyInfo propertyInfo in allProps)
            {
                Assert.AreEqual(propertyInfo.GetValue(clone, null), propertyInfo.GetValue(contentType, null));
            }
        }

        [Test]
        public void Can_Serialize_Member_Type_Without_Error()
        {
            // Arrange
            MemberType contentType = BuildMemberType();

            var json = JsonConvert.SerializeObject(contentType);
            Debug.Print(json);
        }

        private static MemberType BuildMemberType()
        {
            var builder = new MemberTypeBuilder();
            return builder.BuildSimpleMemberType();
        }
    }
}
