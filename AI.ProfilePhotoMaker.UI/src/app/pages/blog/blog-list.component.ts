import { CommonModule, DOCUMENT } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, OnDestroy, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterModule } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { MarketingFooterComponent } from '../../shared/marketing-footer/marketing-footer.component';
import { MarketingHeaderComponent } from '../../shared/marketing-header/marketing-header.component';
import { BlogService, BlogPostSummary } from '../../services/blog.service';
import { blogPosts } from './blog.data';

@Component({
  selector: 'app-blog-list',
  standalone: true,
  imports: [CommonModule, RouterModule, MarketingHeaderComponent, MarketingFooterComponent],
  templateUrl: './blog-list.component.html',
  styleUrls: ['./blog.sass'],
})
export class BlogListComponent implements OnInit, OnDestroy {
  posts: BlogPostSummary[] = [];
  menuPosts: BlogPostSummary[] = [];
  loading = true;

  private readonly _destroy$ = new Subject<void>();
  private readonly _meta = inject(Meta);
  private readonly _title = inject(Title);
  private readonly _document = inject(DOCUMENT);
  private readonly _blogService = inject(BlogService);
  private readonly _cdr = inject(ChangeDetectorRef);

  ngOnInit(): void {
    this.loadPosts();
    this.setMetaTags();
  }

  ngOnDestroy(): void {
    this._destroy$.next();
    this._destroy$.complete();
  }

  private loadPosts(): void {
    this._blogService.getPosts().pipe(takeUntil(this._destroy$)).subscribe({
      next: (data) => {
        this.posts = data;
        this.menuPosts = data.slice(0, 4);
        this.loading = false;
        this._cdr.detectChanges();
      },
      error: () => {
        // Fallback to bundled static data
        const staticData = blogPosts.map(p => ({
          slug: p.slug,
          title: p.title,
          description: p.description,
          dateIso: p.dateIso,
          author: p.author,
          tags: p.tags,
        }));
        this.posts = staticData;
        this.menuPosts = staticData.slice(0, 4);
        this.loading = false;
        this._cdr.detectChanges();
      },
    });
  }

  private setMetaTags(): void {
    const canonicalUrl = 'https://aiprofilephotomaker.com/blog';

    this._title.setTitle('Blog - AI Profile Photo Maker');
    this._meta.updateTag({
      name: 'description',
      content:
        'Practical guides and tips for better AI headshots, LinkedIn photos, and professional profile pictures.',
    });
    this._meta.updateTag({ name: 'robots', content: 'index, follow' });
    this._meta.updateTag({ property: 'og:title', content: 'Blog - AI Profile Photo Maker' });
    this._meta.updateTag({
      property: 'og:description',
      content:
        'Practical guides and tips for better AI headshots, LinkedIn photos, and professional profile pictures.',
    });
    this._meta.updateTag({ property: 'og:type', content: 'website' });
    this._meta.updateTag({ property: 'og:url', content: canonicalUrl });
    this._meta.updateTag({
      property: 'og:image',
      content: 'https://aiprofilephotomaker.com/assets/og-image.png?v=3',
    });
    this._meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this._meta.updateTag({ name: 'twitter:title', content: 'Blog - AI Profile Photo Maker' });
    this._meta.updateTag({
      name: 'twitter:description',
      content:
        'Practical guides and tips for better AI headshots, LinkedIn photos, and professional profile pictures.',
    });
    this._meta.updateTag({
      name: 'twitter:image',
      content: 'https://aiprofilephotomaker.com/assets/og-image.png?v=3',
    });
    this._meta.updateTag({ name: 'twitter:url', content: canonicalUrl });

    this.setCanonicalUrl(canonicalUrl);
  }

  private setCanonicalUrl(url: string): void {
    let canonical = this._document.querySelector("link[rel='canonical']") as HTMLLinkElement | null;
    if (!canonical) {
      canonical = this._document.createElement('link');
      canonical.setAttribute('rel', 'canonical');
      this._document.head.appendChild(canonical);
    }
    canonical.setAttribute('href', url);
  }
}
