import { GalleryComponent } from './gallery.component';
import { GalleryImage } from '../../components/photo-gallery/photo-gallery.component';
import { of } from 'rxjs';

describe('GalleryComponent Studio handoff', () => {
  it('routes the owned Gallery image ID to Studio refinement', () => {
    const router = jasmine.createSpyObj('Router', ['navigate']);
    const component = new GalleryComponent(
      {} as any,
      router,
      {} as any,
      {} as any,
      {} as any,
      {} as any,
      {} as any,
      {} as any
    );
    const image: GalleryImage = {
      id: 42,
      url: 'gallery-image-url',
      title: 'Generated Photo',
      createdAt: new Date(),
      status: 'completed',
      type: 'generated',
    };

    component.onImageRefine(image);

    expect(router.navigate).toHaveBeenCalledOnceWith(['/app/enhance'], {
      queryParams: { refineImageId: image.id },
    });
  });

  it('loads promoted preview images through the authorized download endpoint', async () => {
    const router = jasmine.createSpyObj('Router', ['navigate']);
    const imageBlob = new Blob(['private preview']);
    const headshotGenerationService = jasmine.createSpyObj('HeadshotGenerationService', [
      'getOriginalCandidateImage',
    ]);
    headshotGenerationService.getOriginalCandidateImage.and.returnValue(of(imageBlob));
    const fileUploadService = {
      getUserImages: jasmine.createSpy('getUserImages').and.returnValue(
        of({
          success: true,
          data: {
            totalImages: 2,
            originalUploads: 0,
            generatedImages: 2,
            images: [
              {
                id: 42,
                originalImageUrl: '/profile-images/uploads/source.png',
                processedImageUrl: '/profile-images/generated-private/user/photo.png',
                style: 'Retro Wave',
                createdAt: '2026-09-25T12:32:00Z',
                isOriginalUpload: false,
                isGenerated: true,
                generationMode: 'instant_headshot_promoted_preview',
              },
              {
                id: 43,
                originalImageUrl: '/profile-images/uploads/source.png',
                processedImageUrl: '/profile-images/generated/user/photo.png',
                style: 'Retro Wave',
                createdAt: '2026-09-25T12:31:00Z',
                isOriginalUpload: false,
                isGenerated: true,
                generationMode: 'instant_headshot',
              },
            ],
          },
        })
      ),
    };
    const logger = {
      conditionalLog: jasmine.createSpy('conditionalLog'),
      warn: jasmine.createSpy('warn'),
    };
    const component = new GalleryComponent(
      {} as any,
      router,
      {} as any,
      fileUploadService as any,
      { detectChanges: jasmine.createSpy('detectChanges') } as any,
      {} as any,
      logger as any,
      headshotGenerationService
    );
    spyOn(URL, 'createObjectURL').and.returnValue('blob:authorized-preview');

    await component.loadImages();

    expect(headshotGenerationService.getOriginalCandidateImage).toHaveBeenCalledOnceWith(42);
    expect(component.galleryImages[0].url).toBe('blob:authorized-preview');
    expect(component.galleryImages[0].thumbnailUrl).toBe('blob:authorized-preview');
    expect(component.galleryImages[0].downloadUrl).toBe('blob:authorized-preview');
    expect(component.galleryImages[1].url).toBe('/profile-images/generated/user/photo.png');
    expect(URL.createObjectURL).toHaveBeenCalledOnceWith(imageBlob);
  });
});
