# EasyPortAnalyzer

**EasyPortAnalyzer** is a robust and user-friendly console application written in C# to perform port scanning operations. It allows users to check the availability of TCP and UDP ports over a network and provides detailed results in a simple and interactive way. Whether you're a network administrator or an enthusiast, this tool is designed to help you analyze open and closed ports efficiently.

---

## Features

- **Flexible Port Scanning Options**:
  - Well-Known Ports (0–1023)
  - Registered Ports (1024–49151)
  - Dynamic/Private Ports (49152–65535)
  - Custom Ranges
  - Specific Ports

- **Detailed Results**:
  - Displays open and closed ports for both TCP and UDP protocols.
  - Results can be toggled between all ports and open ports.

- **Export Capabilities**:
  - Save scan results to a CSV file for further analysis.

- **Interactive Scrolling**:
  - Navigate through results using arrow keys.
  - Spacebar toggles between all and open ports.

- **High Performance**:
  - Concurrent scanning using asynchronous tasks.

- **Cross-Platform Expansion** (Upcoming):
  - Future plans include Linux and macOS support.

---

## Getting Started

### Prerequisites

- .NET 9.0 or later is required to run the application.

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/jonavarro22/EasyPortAnalyzer.git
   ```
2. Navigate to the project directory:
   ```bash
   cd EasyPortAnalyzer
   ```
3. Build the application:
   ```bash
   dotnet build
   ```

---

## Usage

1. Run the application:
   ```bash
   dotnet run
   ```
2. Follow the interactive prompts:
   - Enter the target IP or hostname.
   - Choose a port range or specific ports to scan.
3. Review the scan results in the console.
4. Save results to a CSV file by pressing `S`.

---

## Example Output

```plaintext
Results for 192.168.1.1:
Port    TCP     UDP
22      Closed  Open
80      Open    Closed
443     Open    Closed
...
```

---

## Contributing

Contributions are welcome! Feel free to fork the repository and submit a pull request.

---

## License

This project is licensed under the MIT License. See the LICENSE file for details.

