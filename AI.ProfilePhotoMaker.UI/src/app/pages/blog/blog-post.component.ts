import { CommonModule, DOCUMENT } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { MarketingFooterComponent } from '../../shared/marketing-footer/marketing-footer.component';
import { MarketingHeaderComponent } from '../../shared/marketing-header/marketing-header.component';
import { BlogService, BlogPostDetail, BlogPostSummary } from '../../services/blog.service';
import { blogPosts, getBlogPost } from './blog.data';

@Component({
  selector: 'app-blog-post',
  standalone: true,
  imports: [CommonModule, RouterModule, MarketingHeaderComponent, MarketingFooterComponent],
  template: `
    <app-marketing-header></app-marketing-header>

    <main class="blog-page" *ngIf="!loading && post as p; else loadingOrNotFound">
      <article class="blog-post">
        <header class="blog-post-header">
          <a routerLink="/blog" class="back-link"><span aria-hidden="true">←</span> Back to blog</a>
          <time [attr.datetime]="p.dateIso">{{ p.dateIso | date: 'longDate' }}</time>
          <h1>{{ p.title }}</h1>
          <p class="post-description">{{ p.description }}</p>
          <div class="tags">
            <span class="tag" *ngFor="let tag of p.tags">{{ tag }}</span>
          </div>
        </header>

        <section class="blog-content-card">
          <div class="post-content" [innerHTML]="p.contentHtml"></div>
        </section>

        <nav class="blog-menu-block blog-menu-block--footer" aria-label="More blog articles">
          <p class="blog-menu-title">More from the blog</p>
          <div class="blog-menu-links">
            <a class="blog-menu-link" routerLink="/blog">All articles</a>
            <a
              class="blog-menu-link"
              *ngFor="let item of menuPosts"
              [routerLink]="['/blog', item.slug]"
            >
              {{ item.title }}
            </a>
          </div>
        </nav>
      </article>
    </main>

    <ng-template #loadingOrNotFound>
      <main class="blog-page" *ngIf="loading">
        <div class="blog-loading">Loading post...</div>
      </main>
      <main class="blog-page" *ngIf="!loading && !post">
        <article class="blog-post blog-empty-state">
          <h1>Post not found</h1>
          <a routerLink="/blog" class="back-link">Back to blog</a>
        </article>
      </main>
    </ng-template>

    <app-marketing-footer></app-marketing-footer>
  `,
  styleUrls: ['./blog.sass'],
})
export class BlogPostComponent implements OnInit {
  post?: BlogPostDetail;
  menuPosts: BlogPostSummary[] = [];
  loading = true;

  private readonly _route = inject(ActivatedRoute);
  private readonly _meta = inject(Meta);
  private readonly _title = inject(Title);
  private readonly _document = inject(DOCUMENT);
  private readonly _blogService = inject(BlogService);

  ngOnInit(): void {
    const slug = this._route.snapshot.paramMap.get('slug') ?? '';

    this._blogService.getPost(slug).subscribe({
      next: (data) => {
        if (data) {
          this.post = data;
          this.setPostMeta(data);
        } else {
          this.setNotFoundMeta();
        }
        this.loading = false;
      },
      error: () => {
        // Fallback to static data
        const staticPost = getBlogPost(slug);
        if (staticPost) {
          this.post = {
            slug: staticPost.slug,
            title: staticPost.title,
            description: staticPost.description,
            dateIso: staticPost.dateIso,
            author: staticPost.author,
            tags: staticPost.tags,
            contentHtml: staticPost.contentHtml,
            contentMarkdown: '',
          };
          this.setPostMeta(this.post);
        } else {
          this.setNotFoundMeta();
        }
        this.loading = false;
      },
    });

    // Load menu posts separately
    this._blogService.getPosts().subscribe({
      next: (posts) => {
        this.menuPosts = posts.filter(p => p.slug !== slug).slice(0, 3);
      },
    });
  }

  private setPostMeta(post: BlogPostDetail): void {
    const canonicalUrl = `https://aiprofilephotomaker.com/blog/${post.slug}`;

    this._title.setTitle(`${post.title} - AI Profile Photo Maker`);
    this._meta.updateTag({ name: 'description', content: post.description });
    this._meta.updateTag({ name: 'robots', content: 'index, follow' });
    this._meta.updateTag({ property: 'og:title', content: post.title });
    this._meta.updateTag({ property: 'og:description', content: post.description });
    this._meta.updateTag({ property: 'og:type', content: 'article' });
    this._meta.updateTag({ property: 'og:url', content: canonicalUrl });
    this._meta.updateTag({
      property: 'og:image',
      content: 'https://aiprofilephotomaker.com/assets/og-image.png?v=3',
    });
    this._meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this._meta.updateTag({ name: 'twitter:title', content: post.title });
    this._meta.updateTag({ name: 'twitter:description', content: post.description });
    this._meta.updateTag({
      name: 'twitter:image',
      content: 'https://aiprofilephotomaker.com/assets/og-image.png?v=3',
    });
    this._meta.updateTag({ name: 'twitter:url', content: canonicalUrl });

    this.setCanonicalUrl(canonicalUrl);
  }

  private setNotFoundMeta(): void {
    this._title.setTitle('Blog Post Not Found - AI Profile Photo Maker');
    this._meta.updateTag({
      name: 'description',
      content: 'The requested blog post could not be found.',
    });
    this._meta.updateTag({ name: 'robots', content: 'noindex, follow' });
    this.setCanonicalUrl('https://aiprofilephotomaker.com/blog');
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
