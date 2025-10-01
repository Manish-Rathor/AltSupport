using Alt_Support.Models;
using Alt_Support.Configuration;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace Alt_Support.Services
{
    public interface ISimilarityService
    {
        double CalculateSimilarity(TicketInfo ticket1, TicketInfo ticket2);
        List<SimilarityResult> FindSimilarTickets(TicketAnalysisRequest request, List<TicketInfo> historicalTickets);
        double CalculateTextSimilarity(string text1, string text2);
        double CalculateFilePathSimilarity(List<string> files1, List<string> files2);
    }

    public class SimilarityService : ISimilarityService
    {
        private readonly SimilarityConfiguration _config;
        private readonly ILogger<SimilarityService> _logger;

        public SimilarityService(IOptions<ApplicationConfiguration> config, ILogger<SimilarityService> logger)
        {
            _config = config.Value.Similarity;
            _logger = logger;
        }

        public double CalculateSimilarity(TicketInfo ticket1, TicketInfo ticket2)
        {
            var titleSimilarity = CalculateTextSimilarity(ticket1.Title, ticket2.Title);
            var descriptionSimilarity = CalculateTextSimilarity(ticket1.Description, ticket2.Description);
            var filePathSimilarity = CalculateFilePathSimilarity(ticket1.AffectedFiles, ticket2.AffectedFiles);
            var labelSimilarity = CalculateLabelSimilarity(ticket1.Labels, ticket2.Labels);

            var weightedScore = 
                (titleSimilarity * _config.TitleWeight) +
                (descriptionSimilarity * _config.DescriptionWeight) +
                (filePathSimilarity * _config.FilePathWeight) +
                (labelSimilarity * _config.LabelWeight);

            return Math.Round(weightedScore, 3);
        }

        public List<SimilarityResult> FindSimilarTickets(TicketAnalysisRequest request, List<TicketInfo> historicalTickets)
        {
            var results = new List<SimilarityResult>();
            var requestTicket = new TicketInfo
            {
                Title = request.Title,
                Description = request.Description,
                AffectedFiles = request.AffectedFiles,
                ProjectKey = request.ProjectKey
            };

            foreach (var historicalTicket in historicalTickets)
            {
                // Skip tickets from the same project if not specifically requested
                if (!string.IsNullOrEmpty(request.ProjectKey) && 
                    historicalTicket.ProjectKey != request.ProjectKey)
                {
                    continue;
                }

                var similarity = CalculateSimilarity(requestTicket, historicalTicket);
                
                if (similarity >= request.MinimumSimilarityThreshold)
                {
                    var matchReason = GenerateMatchReason(requestTicket, historicalTicket, similarity);
                    
                    results.Add(new SimilarityResult
                    {
                        TicketKey = historicalTicket.TicketKey,
                        Title = historicalTicket.Title,
                        Description = historicalTicket.Description,
                        SimilarityScore = similarity,
                        MatchReason = matchReason,
                        CreatedDate = historicalTicket.CreatedDate,
                        ResolvedDate = historicalTicket.ResolvedDate,
                        Status = historicalTicket.Status,
                        Resolution = historicalTicket.Resolution,
                        AffectedFiles = historicalTicket.AffectedFiles,
                        PullRequestUrl = historicalTicket.PullRequestUrl
                    });
                }
            }

            // Sort by similarity score descending and take top results
            return results
                .OrderByDescending(r => r.SimilarityScore)
                .Take(request.MaxResults)
                .ToList();
        }

        public double CalculateTextSimilarity(string text1, string text2)
        {
            if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2))
                return 0.0;

            // Normalize texts
            var normalizedText1 = NormalizeText(text1);
            var normalizedText2 = NormalizeText(text2);

            // Calculate Jaccard similarity with word n-grams
            var words1 = GetWords(normalizedText1);
            var words2 = GetWords(normalizedText2);

            if (words1.Count == 0 || words2.Count == 0)
                return 0.0;

            // Calculate word-level Jaccard similarity
            var wordSet1 = words1.ToHashSet();
            var wordSet2 = words2.ToHashSet();
            var wordSimilarity = CalculateJaccardSimilarity(wordSet1, wordSet2);

            // Calculate bigram similarity for better context matching
            var bigrams1 = GetBigrams(words1);
            var bigrams2 = GetBigrams(words2);
            var bigramSet1 = bigrams1.ToHashSet();
            var bigramSet2 = bigrams2.ToHashSet();
            var bigramSimilarity = CalculateJaccardSimilarity(bigramSet1, bigramSet2);

            // Combine word and bigram similarities
            return (wordSimilarity * 0.7) + (bigramSimilarity * 0.3);
        }

        public double CalculateFilePathSimilarity(List<string> files1, List<string> files2)
        {
            if (!files1.Any() || !files2.Any())
                return 0.0;

            var normalizedFiles1 = files1.Select(NormalizeFilePath).ToHashSet();
            var normalizedFiles2 = files2.Select(NormalizeFilePath).ToHashSet();

            // Direct file name matches
            var exactMatches = normalizedFiles1.Intersect(normalizedFiles2).Count();
            if (exactMatches > 0)
            {
                return Math.Min(1.0, (double)exactMatches / Math.Max(normalizedFiles1.Count, normalizedFiles2.Count));
            }

            // Partial path matches
            var partialMatchScore = 0.0;
            foreach (var file1 in normalizedFiles1)
            {
                foreach (var file2 in normalizedFiles2)
                {
                    var pathSimilarity = CalculatePathSimilarity(file1, file2);
                    partialMatchScore = Math.Max(partialMatchScore, pathSimilarity);
                }
            }

            return partialMatchScore;
        }

        private double CalculateLabelSimilarity(List<string> labels1, List<string> labels2)
        {
            if (!labels1.Any() || !labels2.Any())
                return 0.0;

            var normalizedLabels1 = labels1.Select(l => l.ToLowerInvariant()).ToHashSet();
            var normalizedLabels2 = labels2.Select(l => l.ToLowerInvariant()).ToHashSet();

            return CalculateJaccardSimilarity(normalizedLabels1, normalizedLabels2);
        }

        private string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            // Convert to lowercase
            text = text.ToLowerInvariant();

            // Remove special characters and extra whitespace
            text = Regex.Replace(text, @"[^\w\s]", " ");
            text = Regex.Replace(text, @"\s+", " ");

            return text.Trim();
        }

        private string NormalizeFilePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return "";

            // Normalize path separators and convert to lowercase
            return filePath.Replace('\\', '/').ToLowerInvariant();
        }

        private List<string> GetWords(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            return text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                      .Where(word => word.Length > 2) // Filter out very short words
                      .ToList();
        }

        private List<string> GetBigrams(List<string> words)
        {
            var bigrams = new List<string>();
            for (int i = 0; i < words.Count - 1; i++)
            {
                bigrams.Add($"{words[i]} {words[i + 1]}");
            }
            return bigrams;
        }

        private double CalculateJaccardSimilarity<T>(ISet<T> set1, ISet<T> set2)
        {
            if (set1.Count == 0 && set2.Count == 0)
                return 1.0;

            var intersection = set1.Intersect(set2).Count();
            var union = set1.Union(set2).Count();

            return union == 0 ? 0.0 : (double)intersection / union;
        }

        private double CalculatePathSimilarity(string path1, string path2)
        {
            var parts1 = path1.Split('/').Where(p => !string.IsNullOrEmpty(p)).ToList();
            var parts2 = path2.Split('/').Where(p => !string.IsNullOrEmpty(p)).ToList();

            if (!parts1.Any() || !parts2.Any())
                return 0.0;

            // Check if file names are similar
            var fileName1 = parts1.LastOrDefault() ?? "";
            var fileName2 = parts2.LastOrDefault() ?? "";
            
            if (fileName1 == fileName2)
                return 0.8; // High similarity for same file name

            // Check directory structure similarity
            var dirParts1 = parts1.Take(parts1.Count - 1).ToList();
            var dirParts2 = parts2.Take(parts2.Count - 1).ToList();

            var commonDirs = dirParts1.Intersect(dirParts2).Count();
            var totalDirs = Math.Max(dirParts1.Count, dirParts2.Count);

            if (totalDirs == 0)
                return 0.0;

            return (double)commonDirs / totalDirs * 0.5; // Lower weight for directory similarity
        }

        private string GenerateMatchReason(TicketInfo newTicket, TicketInfo historicalTicket, double similarity)
        {
            var reasons = new List<string>();

            var titleSim = CalculateTextSimilarity(newTicket.Title, historicalTicket.Title);
            if (titleSim > 0.5)
                reasons.Add($"Similar title ({titleSim:P0})");

            var descSim = CalculateTextSimilarity(newTicket.Description, historicalTicket.Description);
            if (descSim > 0.3)
                reasons.Add($"Similar description ({descSim:P0})");

            var fileSim = CalculateFilePathSimilarity(newTicket.AffectedFiles, historicalTicket.AffectedFiles);
            if (fileSim > 0.5)
                reasons.Add($"Common affected files ({fileSim:P0})");

            if (!reasons.Any())
                reasons.Add("General similarity");

            return string.Join(", ", reasons) + $" (Overall: {similarity:P0})";
        }
    }
}