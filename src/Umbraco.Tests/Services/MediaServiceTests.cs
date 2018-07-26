﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Rdbms;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.DatabaseModelDefinitions;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Persistence.UnitOfWork;
using Umbraco.Tests.TestHelpers;
using Umbraco.Tests.TestHelpers.Entities;

namespace Umbraco.Tests.Services
{
    [DatabaseTestBehavior(DatabaseBehavior.NewDbFileAndSchemaPerTest)]
    [TestFixture, RequiresSTA]
    public class MediaServiceTests : BaseServiceTest
    {
        [SetUp]
        public override void Initialize()
        {
            base.Initialize();
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        [Test]
        public void Get_Paged_Children_With_Media_Type_Filter()
        {
            var mediaService = ServiceContext.MediaService;
            var mediaType1 = MockedContentTypes.CreateImageMediaType("Image2");
            ServiceContext.ContentTypeService.Save(mediaType1);
            var mediaType2 = MockedContentTypes.CreateImageMediaType("Image3");
            ServiceContext.ContentTypeService.Save(mediaType2);

            for (int i = 0; i < 10; i++)
            {
                var m1 = MockedMedia.CreateMediaImage(mediaType1, -1);
                mediaService.Save(m1);
                var m2 = MockedMedia.CreateMediaImage(mediaType2, -1);
                mediaService.Save(m2);
            }

            long total;
            var result = ServiceContext.MediaService.GetPagedChildren(-1, 0, 11, out total, "SortOrder", Direction.Ascending, true, null, new[] {mediaType1.Id, mediaType2.Id});
            Assert.AreEqual(11, result.Count());
            Assert.AreEqual(20, total);

            result = ServiceContext.MediaService.GetPagedChildren(-1, 1, 11, out total, "SortOrder", Direction.Ascending, true, null, new[] { mediaType1.Id, mediaType2.Id });
            Assert.AreEqual(9, result.Count());
            Assert.AreEqual(20, total);
        }

        [Test]
        public void Can_Move_Media()
        {
            // Arrange
            var mediaItems = CreateTrashedTestMedia();
            var mediaService = ServiceContext.MediaService;
            var media = mediaService.GetById(mediaItems.Item3.Id);

            // Act
            mediaService.Move(media, mediaItems.Item2.Id);

            // Assert
            Assert.That(media.ParentId, Is.EqualTo(mediaItems.Item2.Id));
            Assert.That(media.Trashed, Is.False);
        }

        [Test]
        public void Can_Move_Media_To_RecycleBin()
        {
            // Arrange
            var mediaItems = CreateTrashedTestMedia();
            var mediaService = ServiceContext.MediaService;
            var media = mediaService.GetById(mediaItems.Item1.Id);

            // Act
            mediaService.MoveToRecycleBin(media);

            // Assert
            Assert.That(media.ParentId, Is.EqualTo(-21));
            Assert.That(media.Trashed, Is.True);
        }

        [Test]
        public void Can_Move_Media_From_RecycleBin()
        {
            // Arrange
            var mediaItems = CreateTrashedTestMedia();
            var mediaService = ServiceContext.MediaService;
            var media = mediaService.GetById(mediaItems.Item4.Id);

            // Act - moving out of recycle bin
            mediaService.Move(media, mediaItems.Item1.Id);
            var mediaChild = mediaService.GetById(mediaItems.Item5.Id);

            // Assert
            Assert.That(media.ParentId, Is.EqualTo(mediaItems.Item1.Id));
            Assert.That(media.Trashed, Is.False);
            Assert.That(mediaChild.ParentId, Is.EqualTo(mediaItems.Item4.Id));
            Assert.That(mediaChild.Trashed, Is.False);
        }

        [Test]
        public void Cannot_Save_Media_With_Empty_Name()
        {
            // Arrange
            var mediaService = ServiceContext.MediaService;
            var mediaType = MockedContentTypes.CreateVideoMediaType();
            ServiceContext.ContentTypeService.Save(mediaType);
            var media = mediaService.CreateMedia(string.Empty, -1, "video");

            // Act & Assert
            Assert.Throws<ArgumentException>(() => mediaService.Save(media));
        }

        [Test]
        public void Ensure_Content_Xml_Created()
        {
            var mediaService = ServiceContext.MediaService;
            var mediaType = MockedContentTypes.CreateVideoMediaType();
            ServiceContext.ContentTypeService.Save(mediaType);
            var media = mediaService.CreateMedia("Test", -1, "video");

            mediaService.Save(media);

            var provider = new PetaPocoUnitOfWorkProvider(Logger);
            using (var uow = provider.GetUnitOfWork())
            {
                Assert.IsTrue(uow.Database.Exists<ContentXmlDto>(media.Id));
            }
        }

        [Test]
        public void Can_Get_Media_By_Path()
        {
            var mediaService = ServiceContext.MediaService;
            var mediaType = MockedContentTypes.CreateImageMediaType("Image2");
            ServiceContext.ContentTypeService.Save(mediaType);

            var media = MockedMedia.CreateMediaImage(mediaType, -1);
            mediaService.Save(media);

            var mediaPath = "/media/test-image.png";
            var resolvedMedia = mediaService.GetMediaByPath(mediaPath);
            
            Assert.IsNotNull(resolvedMedia);
            Assert.That(resolvedMedia.GetValue(Constants.Conventions.Media.File).ToString() == mediaPath);
        }

        [Test]
        public void Can_Get_Media_With_Crop_By_Path()
        {
            var mediaService = ServiceContext.MediaService;
            var mediaType = MockedContentTypes.CreateImageMediaType("Image2");
            ServiceContext.ContentTypeService.Save(mediaType);

            var media = MockedMedia.CreateMediaImageWithCrop(mediaType, -1);
            mediaService.Save(media);

            var mediaPath = "/media/test-image.png";
            var resolvedMedia = mediaService.GetMediaByPath(mediaPath);

            Assert.IsNotNull(resolvedMedia);
            Assert.That(resolvedMedia.GetValue(Constants.Conventions.Media.File).ToString().Contains(mediaPath));
        }

        [Test]
        public void Can_Get_Paged_Children()
        {
            var mediaType = MockedContentTypes.CreateImageMediaType("Image2");
            ServiceContext.ContentTypeService.Save(mediaType);
            for (int i = 0; i < 10; i++)
            {
                var c1 = MockedMedia.CreateMediaImage(mediaType, -1);
                ServiceContext.MediaService.Save(c1);
            }

            var service = ServiceContext.MediaService;

            long total;
            var entities = service.GetPagedChildren(-1, 0, 6, out total).ToArray();
            Assert.That(entities.Length, Is.EqualTo(6));
            Assert.That(total, Is.EqualTo(10));
            entities = service.GetPagedChildren(-1, 1, 6, out total).ToArray();
            Assert.That(entities.Length, Is.EqualTo(4));
            Assert.That(total, Is.EqualTo(10));
        }

        [Test]
        public void Can_Get_Paged_Children_Dont_Get_Descendants()
        {
            var mediaType = MockedContentTypes.CreateImageMediaType("Image2");
            ServiceContext.ContentTypeService.Save(mediaType);
            // only add 9 as we also add a folder with children
            for (int i = 0; i < 9; i++)
            {
                var m1 = MockedMedia.CreateMediaImage(mediaType, -1);
                ServiceContext.MediaService.Save(m1);
            }

            var mediaTypeForFolder = MockedContentTypes.CreateImageMediaType("Folder2");
            ServiceContext.ContentTypeService.Save(mediaTypeForFolder);
            var mediaFolder = MockedMedia.CreateMediaFolder(mediaTypeForFolder, -1);
            ServiceContext.MediaService.Save(mediaFolder);
            for (int i = 0; i < 10; i++)
            {
                var m1 = MockedMedia.CreateMediaImage(mediaType, mediaFolder.Id);
                ServiceContext.MediaService.Save(m1);
            }

            var service = ServiceContext.MediaService;

            long total;
            // children in root including the folder - not the descendants in the folder
            var entities = service.GetPagedChildren(-1, 0, 6, out total).ToArray();
            Assert.That(entities.Length, Is.EqualTo(6));
            Assert.That(total, Is.EqualTo(10));
            entities = service.GetPagedChildren(-1, 1, 6, out total).ToArray();
            Assert.That(entities.Length, Is.EqualTo(4));
            Assert.That(total, Is.EqualTo(10));

            // children in folder
            entities = service.GetPagedChildren(mediaFolder.Id, 0, 6, out total).ToArray();
            Assert.That(entities.Length, Is.EqualTo(6));
            Assert.That(total, Is.EqualTo(10));
            entities = service.GetPagedChildren(mediaFolder.Id, 1, 6, out total).ToArray();
            Assert.That(entities.Length, Is.EqualTo(4));
            Assert.That(total, Is.EqualTo(10));
        }

        private Tuple<IMedia, IMedia, IMedia, IMedia, IMedia> CreateTrashedTestMedia()
        {
            //Create and Save folder-Media -> 1050
            var folderMediaType = ServiceContext.ContentTypeService.GetMediaType(1031);
            var folder = MockedMedia.CreateMediaFolder(folderMediaType, -1);
            ServiceContext.MediaService.Save(folder);
            
            //Create and Save folder-Media -> 1051
            var folder2 = MockedMedia.CreateMediaFolder(folderMediaType, -1);
            ServiceContext.MediaService.Save(folder2);
            
            //Create and Save image-Media  -> 1052
            var imageMediaType = ServiceContext.ContentTypeService.GetMediaType(1032);
            var image = (Media)MockedMedia.CreateMediaImage(imageMediaType, 1050);
            ServiceContext.MediaService.Save(image);
            
            //Create and Save folder-Media that is trashed -> 1053
            var folderTrashed = (Media)MockedMedia.CreateMediaFolder(folderMediaType, -21);
            folderTrashed.Trashed = true;
            ServiceContext.MediaService.Save(folderTrashed);
            
            //Create and Save image-Media child of folderTrashed -> 1054            
            var imageTrashed = (Media)MockedMedia.CreateMediaImage(imageMediaType, folderTrashed.Id);
            imageTrashed.Trashed = true;
            ServiceContext.MediaService.Save(imageTrashed);


            return new Tuple<IMedia, IMedia, IMedia, IMedia, IMedia>(folder, folder2, image, folderTrashed, imageTrashed);
        }
    }
}
