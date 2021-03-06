﻿/*
Copyright (C) 1996-1997 Id Software, Inc.

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  

See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.

*/
// chase.c -- chase camera code

namespace quake
{
    using System;

    public partial class chase
    {
        static cvar_t	chase_back = new cvar_t("chase_back", "100");
        static cvar_t	chase_up = new cvar_t("chase_up", "16");
        static cvar_t	chase_right = new cvar_t("chase_right", "0");
        public static cvar_t	chase_active = new cvar_t("chase_active", "0");

        double[]	chase_pos = new double[3] {0, 0, 0};
        double[]	chase_angles = new double[3] {0, 0, 0};

        static double[]	chase_dest = new double[3] {0, 0, 0};
        double[]	chase_dest_angles = new double[3] {0, 0, 0};
        
        public static void Chase_Init ()
        {
	        cvar_t.Cvar_RegisterVariable (chase_back);
	        cvar_t.Cvar_RegisterVariable (chase_up);
	        cvar_t.Cvar_RegisterVariable (chase_right);
	        cvar_t.Cvar_RegisterVariable (chase_active);
        }

        static void Chase_Reset ()
        {
	        // for respawning and teleporting
        //	start position 12 units behind head
        }

        static void TraceLine (double[] start, double[] end, double[] impact)
        {
            world.trace_t trace;

            trace = new world.trace_t();
            world.SV_RecursiveHullCheck(client.cl.worldmodel.hulls[0], 0, 0, 1, start, end, trace);

            mathlib.VectorCopy(trace.endpos, impact);
        }

        public static void Chase_Update ()
        {
	        int		    i;
	        double	    dist;
            double[]    forward = new double[3] {0, 0, 0}, up = new double[3] {0, 0, 0}, right = new double[3] {0, 0, 0};
            double[]    dest = new double[3] {0, 0, 0}, stop = new double[3] {0, 0, 0};

            // if can't see player, reset
            mathlib.AngleVectors(client.cl.viewangles, forward, right, up);

            // calc exact destination
            for (i = 0; i < 3; i++)
                chase_dest[i] = render.r_refdef.vieworg[i]
                - forward[i] * chase_back.value
                - right[i] * chase_right.value;
            chase_dest[2] = render.r_refdef.vieworg[2] + chase_up.value;

            // find the spot the player is looking at
            mathlib.VectorMA(render.r_refdef.vieworg, 4096, forward, dest);
            TraceLine(render.r_refdef.vieworg, dest, stop);

            // calculate pitch to look at the same spot from camera
            mathlib.VectorSubtract(stop,  render.r_refdef.vieworg, stop);
            dist = mathlib.DotProduct(stop, forward);
            if (dist < 1)
                dist = 1;
           render. r_refdef.viewangles[quakedef.PITCH] = -Math.Atan(stop[2] / dist) / mathlib.M_PI * 180;

            // move towards destination
           TraceLine(render.r_refdef.vieworg, chase_dest, stop);
            if (mathlib.Length(stop) != 0)
                mathlib.VectorCopy(stop, chase_dest);
            mathlib.VectorCopy(chase_dest, render.r_refdef.vieworg);
        }
    }
}
