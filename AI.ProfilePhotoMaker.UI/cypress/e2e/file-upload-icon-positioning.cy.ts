/// <reference types="cypress" />

describe('File Upload Icon Positioning Tests', () => {
  beforeEach(() => {
    // Navigate to the dashboard page
    cy.visit('/dashboard')
    
    // Wait for the page to load
    cy.get('.file-upload-section').should('be.visible')
  })

  it('should position info icon within thumbnail bounds', () => {
    // Upload a test image to trigger the file preview
    cy.fixture('test-image.jpg', 'base64').then(fileContent => {
      cy.get('input[type="file"]').selectFile({
        contents: Cypress.Buffer.from(fileContent, 'base64'),
        fileName: 'test-image.jpg',
        mimeType: 'image/jpeg'
      }, { force: true })
    })

    // Wait for quality check to complete
    cy.get('.selected-files-preview').should('be.visible')
    
    // Find a file card with invalid status (red border)
    cy.get('.selected-file-item-enhanced.file-invalid').first().within(() => {
      const thumbnail = cy.get('img').first()
      const infoIcon = cy.get('.file-info-popup .info-btn')
      
      // Get thumbnail bounds
      thumbnail.then(($thumbnail) => {
        const thumbRect = $thumbnail[0].getBoundingClientRect()
        
        // Get info icon bounds
        infoIcon.then(($icon) => {
          const iconRect = $icon[0].getBoundingClientRect()
          
          // Verify icon is positioned within thumbnail bounds
          expect(iconRect.left).to.be.greaterThan(thumbRect.left)
          expect(iconRect.top).to.be.greaterThan(thumbRect.top)
          expect(iconRect.right).to.be.lessThan(thumbRect.right)
          expect(iconRect.bottom).to.be.lessThan(thumbRect.bottom)
        })
      })
    })
  })

  it('should have subtle styling for info icon', () => {
    // Upload a test image
    cy.fixture('test-image.jpg', 'base64').then(fileContent => {
      cy.get('input[type="file"]').selectFile({
        contents: Cypress.Buffer.from(fileContent, 'base64'),
        fileName: 'test-image.jpg',
        mimeType: 'image/jpeg'
      }, { force: true })
    })

    cy.get('.selected-files-preview').should('be.visible')
    
    cy.get('.info-btn').first().should(($btn) => {
      const styles = window.getComputedStyle($btn[0])
      
      // Check for subtle dark background (not bright red)
      expect(styles.backgroundColor).to.contain('rgba(0, 0, 0')
      
      // Check for reduced size (18-22px range)
      const width = parseInt(styles.width)
      expect(width).to.be.at.least(18)
      expect(width).to.be.at.most(22)
      
      // Check for subtle opacity
      expect(parseFloat(styles.opacity)).to.be.lessThan(1)
    })
  })

  it('should have proper hover states', () => {
    // Upload a test image
    cy.fixture('test-image.jpg', 'base64').then(fileContent => {
      cy.get('input[type="file"]').selectFile({
        contents: Cypress.Buffer.from(fileContent, 'base64'),
        fileName: 'test-image.jpg',
        mimeType: 'image/jpeg'
      }, { force: true })
    })

    cy.get('.selected-files-preview').should('be.visible')
    
    // Test hover state
    cy.get('.info-btn').first().trigger('mouseover')
    
    cy.get('.info-btn').first().should(($btn) => {
      const styles = window.getComputedStyle($btn[0])
      
      // Should have full opacity on hover
      expect(parseFloat(styles.opacity)).to.equal(1)
      
      // Should have enhanced shadow
      expect(styles.boxShadow).to.contain('rgba(0, 0, 0, 0.3)')
    })
  })

  it('should toggle popup on click', () => {
    // Upload a test image
    cy.fixture('test-image.jpg', 'base64').then(fileContent => {
      cy.get('input[type="file"]').selectFile({
        contents: Cypress.Buffer.from(fileContent, 'base64'),
        fileName: 'test-image.jpg',
        mimeType: 'image/jpeg'
      }, { force: true })
    })

    cy.get('.selected-files-preview').should('be.visible')
    
    // Click info button
    cy.get('.info-btn').first().click()
    
    // Verify popup appears
    cy.get('.popup-details').should('be.visible')
    
    // Click again to close
    cy.get('.info-btn').first().click()
    
    // Verify popup is hidden
    cy.get('.popup-details').should('not.exist')
  })

  it('should be accessible via keyboard', () => {
    // Upload a test image
    cy.fixture('test-image.jpg', 'base64').then(fileContent => {
      cy.get('input[type="file"]').selectFile({
        contents: Cypress.Buffer.from(fileContent, 'base64'),
        fileName: 'test-image.jpg',
        mimeType: 'image/jpeg'
      }, { force: true })
    })

    cy.get('.selected-files-preview').should('be.visible')
    
    // Focus on info button using tab
    cy.get('.info-btn').first().focus()
    
    // Verify focus styles
    cy.get('.info-btn').first().should('have.focus')
    
    // Press Enter to activate
    cy.get('.info-btn').first().type('{enter}')
    
    // Verify popup appears
    cy.get('.popup-details').should('be.visible')
  })

  it('should maintain positioning across screen sizes', () => {
    // Test mobile viewport
    cy.viewport(375, 667)
    
    // Upload a test image
    cy.fixture('test-image.jpg', 'base64').then(fileContent => {
      cy.get('input[type="file"]').selectFile({
        contents: Cypress.Buffer.from(fileContent, 'base64'),
        fileName: 'test-image.jpg',
        mimeType: 'image/jpeg'
      }, { force: true })
    })

    cy.get('.selected-files-preview').should('be.visible')
    
    // Verify mobile positioning
    cy.get('.selected-file-item-enhanced.file-invalid').first().within(() => {
      cy.get('.file-info-popup .info-btn').should('be.visible')
      
      // Should still be positioned within thumbnail bounds
      const thumbnail = cy.get('img').first()
      const infoIcon = cy.get('.file-info-popup .info-btn')
      
      thumbnail.then(($thumbnail) => {
        const thumbRect = $thumbnail[0].getBoundingClientRect()
        
        infoIcon.then(($icon) => {
          const iconRect = $icon[0].getBoundingClientRect()
          
          expect(iconRect.left).to.be.greaterThan(thumbRect.left)
          expect(iconRect.top).to.be.greaterThan(thumbRect.top)
        })
      })
    })
    
    // Test desktop viewport
    cy.viewport(1920, 1080)
    
    // Verify desktop positioning still works
    cy.get('.selected-file-item-enhanced.file-invalid').first().within(() => {
      cy.get('.file-info-popup .info-btn').should('be.visible')
    })
  })
})