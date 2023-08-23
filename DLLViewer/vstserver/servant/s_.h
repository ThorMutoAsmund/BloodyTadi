/*
    Copyright (C) 2002 Kjetil S. Matheussen / Notam.

    This program is free software; you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 2 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.

*/

#include "clienthandler.h"


void VSTS_dispatcher(
		     struct VSTClient *vstclient,
		     struct VSTS_controlRequest *cr
		     );

void VSTS_getParameter(
		       struct VSTClient *vstclient,
		       struct VSTS_controlRequest *cr
		       );

void VSTS_process(
		  struct VSTClient *vstclient,
		  int sampleframes
		  );
void VSTS_processReplacing(
			   struct VSTClient *vstclient,
			   int sampleframes
			   );
void VSTS_setParameter(
		       struct VSTClient *vstclient,
		       struct VSTS_controlRequest *cr
		       );


void s_effProcessEvents(
			struct VSTClient *vstclient,
			struct VSTS_controlRequest *cr
			);
