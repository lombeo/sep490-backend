using System;
using System.Collections.Generic;

namespace Sep490_Backend.DTO.Project
{
    public class ProjectStatisticsDTO
    {
        public List<ProjectStatisticItemDTO> Statistics { get; set; } = new List<ProjectStatisticItemDTO>();
    }

    public class ProjectStatisticItemDTO
    {
        public string Label { get; set; }
        public decimal Value { get; set; }
    }
} 