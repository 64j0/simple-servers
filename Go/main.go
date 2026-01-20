package main

// go run main.go

import (
	"github.com/gin-gonic/gin"
)

func main() {
	// 1. Create a default Gin router with logging and recovery middleware
	r := gin.Default()

	// 2. Define the GET /hello endpoint
	r.GET("/hello", func(c *gin.Context) {
		// Returns a plain string with a 200 OK status
		c.String(200, "Hellofrom Gin (Go lang)!")
	})

	// 3. Run the server (default is port 5000)
	r.Run(":5000")
}
